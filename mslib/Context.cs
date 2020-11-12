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
            var connStrObj = ConfigurationManager.ConnectionStrings[connStrName];
            if (connStrObj == null)
                throw new ConfigurationErrorsException("metastrings connection string missing from config");

            var connStr = connStrObj.ConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
                throw new ConfigurationErrorsException("metastrings connection string empty in config");

            Db = new MySqlDb(connStr);
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

        public Command Cmd => new Command(this);

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

        public async Task<long> ExecScalar64Async(Select select)
        {
            long val = Utils.ConvertDbInt64(await ExecScalarAsync(select).ConfigureAwait(false));
            return val;
        }

        public async Task<int> ExecScalar32Async(Select select)
        {
            int val = Utils.ConvertDbInt32(await ExecScalarAsync(select).ConfigureAwait(false));
            return val;
        }

        public async Task<List<T>> ExecListAsync<T>(Select select)
        {
            var values = new List<T>();
            using (var reader = await ExecSelectAsync(select))
            {
                while (await reader.ReadAsync())
                    values.Add((T)reader.GetValue(0));
            }
            return values;
        }

        public async Task<long> GetRowIdAsync(string tableName, object key)
        {
            Utils.ValidateTableName(tableName, "GetRowId");
            Select select = Sql.Parse($"SELECT id FROM {tableName} WHERE value = @value");
            select.AddParam("@value", key);
            long id = await ExecScalar64Async(select).ConfigureAwait(false);
            return id;
        }

        public async Task<object> GetRowValueAsync(string tableName, long id)
        {
            Utils.ValidateTableName(tableName, "GetRowValueAsync");
            Select select = Sql.Parse($"SELECT value FROM {tableName} WHERE id = @id");
            select.AddParam("@id", id);
            object val = await ExecScalarAsync(select).ConfigureAwait(false);
            return val;
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

        private static object sm_dbBuildLock = new object();
        private static ConcurrentDictionary<string, string> sm_dbConnStrs = new ConcurrentDictionary<string, string>();
    }
}
