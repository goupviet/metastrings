using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.Common;

namespace metastrings
{
    /// <summary>
    /// Context manages the MySQL database connection
    /// and provides many useful query helper functions
    /// </summary>
    public class Context : IDisposable
    {
        /// <summary>
        /// Create a context for a MySQL database connection
        /// </summary>
        /// <param name="connStrName">Name of the database connections string in the config file</param>
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

        /// <summary>
        /// Clean up the database connection
        /// </summary>
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

        /// <summary>
        /// The MySQL database connection
        /// </summary>
        public IDb Db { get; private set; }

        /// <summary>
        /// Create a new Command object using this Context
        /// </summary>
        public Command Cmd => new Command(this);

        /// <summary>
        /// Transactions are supported, 
        /// but should not be used around any code affecting data 
        /// in the Table, Name, Value metastrings database
        /// </summary>
        /// <param name="level">Transaction isolation level</param>
        /// <returns></returns>
        public MsTrans BeginTrans(IsolationLevel level = IsolationLevel.Unspecified)
        {
            return Db.BeginTrans(level);
        }

        /// <summary>
        /// Query helper function to get a read for a query
        /// </summary>
        /// <param name="select">Query to execute</param>
        /// <returns>Reader to get results from</returns>
        public async Task<DbDataReader> ExecSelectAsync(Select select)
        {
            var cmdParams = select.cmdParams;
            var sql = await Sql.GenerateSqlAsync(this, select).ConfigureAwait(false);
            return await Db.ExecuteReaderAsync(sql, cmdParams).ConfigureAwait(false);
        }

        /// <summary>
        /// Query helper function to get a single value for a query
        /// </summary>
        /// <param name="select">Query to execute</param>
        /// <returns>The single query result value</returns>
        public async Task<object> ExecScalarAsync(Select select)
        {
            var cmdParams = select.cmdParams;
            var sql = await Sql.GenerateSqlAsync(this, select).ConfigureAwait(false);
            return await Db.ExecuteScalarAsync(sql, cmdParams).ConfigureAwait(false);
        }

        /// <summary>
        /// Query helper to get a single 64-bit integer query result
        /// </summary>
        /// <param name="select">Query to execute</param>
        /// <returns>64-bit result value, or -1 if processing fails</returns>
        public async Task<long> ExecScalar64Async(Select select)
        {
            long val = Utils.ConvertDbInt64(await ExecScalarAsync(select).ConfigureAwait(false));
            return val;
        }

        /// <summary>
        /// Query helper to get a list of results from a single-column query
        /// </summary>
        /// <param name="select">Query to execute</param>
        /// <returns>List of results of type T</returns>
        public async Task<List<T>> ExecListAsync<T>(Select select)
        {
            var values = new List<T>();
            using (var reader = await ExecSelectAsync(select).ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                    values.Add((T)reader.GetValue(0));
            }
            return values;
        }

        /// <summary>
        /// Query helper to get a dictionary of results from a single-column query
        /// </summary>
        /// <param name="select">Query to execute</param>
        /// <returns>List of results of type T</returns>
        public async Task<ListDictionary<K, V>> ExecDictAsync<K, V>(Select select)
        {
            var values = new ListDictionary<K, V>();
            using (var reader = await ExecSelectAsync(select).ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                    values.Add((K)reader.GetValue(0), (V)reader.GetValue(1));
            }
            return values;
        }

        /// <summary>
        /// Get the 64-bit items table row ID for a given table and key
        /// </summary>
        /// <param name="tableName">What table are we looking in?</param>
        /// <param name="key">What is the key to the item in the table?</param>
        /// <returns>64-bit row ID, or -1 if not found</returns>
        public async Task<long> GetRowIdAsync(string tableName, object key)
        {
            Utils.ValidateTableName(tableName, "GetRowId");
            Select select = Sql.Parse($"SELECT id FROM {tableName} WHERE value = @value");
            select.AddParam("@value", key);
            long id = await ExecScalar64Async(select).ConfigureAwait(false);
            return id;
        }

        /// <summary>
        /// Get the object value from the given table and items table ID
        /// </summary>
        /// <param name="tableName">What table are we looking in?</param>
        /// <param name="id">What is the items table row ID we are looking for?</param>
        /// <returns>object value if found, null otherwise</returns>
        public async Task<object> GetRowValueAsync(string tableName, long id)
        {
            Utils.ValidateTableName(tableName, "GetRowValueAsync");
            Select select = Sql.Parse($"SELECT value FROM {tableName} WHERE id = @id");
            select.AddParam("@id", id);
            object val = await ExecScalarAsync(select).ConfigureAwait(false);
            return val;
        }

        /// <summary>
        /// Process all those queries that piled up, used by Command's Define functions
        /// This is a rare case of where using a transaction is well-advised
        /// </summary>
        public async Task ProcessPostOpsAsync()
        {
            if (m_postItemOps.Count == 0)
                return;

            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                using (var msTrans = BeginTrans())
                {
                    foreach (string sql in m_postItemOps)
                        await Db.ExecuteSqlAsync(sql).ConfigureAwait(false);
                    msTrans.Commit();
                }
            }
            finally
            {
                m_postItemOps.Clear();
                ScopeTiming.RecordScope("ProcessItemPostOps", totalTimer);
            }
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
    }
}
