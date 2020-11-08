using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.Common;

namespace metastrings
{
    public class Context : IDisposable
    {
        public Context(string connStrName = "metastrings")
        {
            string dbConnStr;
            if (!sm_dbConnStrs.TryGetValue(connStrName, out dbConnStr))
            {
                lock (sm_dbBuildLock)
                {
                    if (!sm_dbConnStrs.TryGetValue(connStrName, out dbConnStr))
                    {
                        dbConnStr = GetDbConnStr(connStrName);
                        sm_dbConnStrs[connStrName] = dbConnStr;
                    }
                }
            }
            Db = new MySqlDb(dbConnStr);
        }

        public void Dispose()
        {
            if (Db != null)
            {
                Db.Dispose();
                Db = null;
            }

            if (m_postItemOps.Count > 0)
                throw new MetaStringsException("Post ops remain; call ProcessPostOpsAsync before disposing the metastrings context");
        }

        public static void RunSql(IDb db, string[] sqlQueries)
        {
            foreach (string sql in sqlQueries)
                db.ExecuteSql(sql);
        }

        public IDb Db { get; private set; }

        public MsTrans BeginTrans(IsolationLevel level = IsolationLevel.Unspecified)
        {
            return Db.BeginTrans(level);
        }

        public async Task<DbDataReader> ExecSelectAsync(Select select)
        {
            var cmdParams = select.cmdParams;
            var sql = await Sql.GenerateSqlAsync(this, select).ConfigureAwait(false);
            return await Db.ExecuteReaderAsync(sql, cmdParams).ConfigureAwait(false);
        }

        public async Task<object> ExecScalarAsync(Select select)
        {
            var cmdParams = select.cmdParams;
            var sql = await Sql.GenerateSqlAsync(this, select).ConfigureAwait(false);
            return await Db.ExecuteScalarAsync(sql, cmdParams).ConfigureAwait(false);
        }

        public async Task<long> GetRowIdAsync(string tableName, object key)
        {
            Utils.ValidateTableName(tableName, "GetRowId");
            Select select = Sql.Parse($"SELECT id FROM {tableName} WHERE value = @value");
            select.AddParam("@value", key);
            object objId = await ExecScalarAsync(select).ConfigureAwait(false);
            long id = Utils.ConvertDbInt64(objId);
            return id;
        }

        public async Task<int> ProcessPostOpsAsync()
        {
            if (m_postItemOps.Count == 0)
                return 0;

            var totalTimer = ScopeTiming.StartTiming();
            int affected = 0;
            try
            {
                using (var msTrans = BeginTrans())
                {
                    foreach (string sql in m_postItemOps)
                        affected += await Db.ExecuteSqlAsync(sql).ConfigureAwait(false);
                    msTrans.Commit();
                }
            }
            finally
            {
                m_postItemOps.Clear();
                ScopeTiming.RecordScope("ProcessItemPostOps", totalTimer);
            }
            return affected;
        }
        public void AddPostOp(string sql)
        {
            m_postItemOps.Add(sql);
        }
        public void ClearPostOps()
        {
            m_postItemOps.Clear();
        }
        private List<string> m_postItemOps = new List<string>();

        public static string GetDbConnStr(string connStrName = "metastrings")
        {
            var connStrObj = ConfigurationManager.ConnectionStrings[connStrName];
            if (connStrObj == null)
                throw new ConfigurationErrorsException("metastrings connection string missing from config");

            var connStr = connStrObj.ConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
                throw new ConfigurationErrorsException("metastrings connection string empty in config");

            if (!IsDbServer(connStr))
            {
                string dbFilePath = DbConnStrToFilePath(connStr);
                connStr = "Data Source=" + dbFilePath;
            }

            return connStr;
        }

        public static string DbConnStrToFilePath(string connStr)
        {
            if (IsDbServer(connStr))
                throw new MetaStringsException("Connection string is not for file-based DB");

            string filePath = connStr;

            int equals = filePath.IndexOf('=');
            if (equals > 0)
                filePath = filePath.Substring(equals + 1);

            filePath = filePath.Replace("[UserRoaming]", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Trim('\\') + "\\");
            return filePath;
        }

        public static bool IsDbServer(string connStr)
        {
            bool isServer = connStr.IndexOf("Server=", 0, StringComparison.OrdinalIgnoreCase) >= 0;
            return isServer;
        }

        public bool IsServerDb { get; private set; }

        private static object sm_dbBuildLock = new object();
        private static ConcurrentDictionary<string, string> sm_dbConnStrs = new ConcurrentDictionary<string, string>();
    }
}
