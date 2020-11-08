using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;

using MySql.Data.MySqlClient;

namespace metastrings
{
    public class MySqlDb : IDb
    {
        public MySqlDb(string dbConnStr)
        {
            DbConn = new MySqlConnection(dbConnStr);
            DbConn.Open();
        }

        public void Dispose()
        {
            if (DbTrans != null)
            {
                DbTrans.Dispose();
                DbTrans = null;
            }

            if (DbConn != null)
            {
                DbConn.Dispose();
                DbConn = null;
            }
        }

        public MsTrans BeginTrans(IsolationLevel level = IsolationLevel.Unspecified)
        {
            if (DbTrans == null)
            {
                if (level == IsolationLevel.Unspecified)
                    DbTrans = DbConn.BeginTransaction();
                else
                    DbTrans = DbConn.BeginTransaction(level);
            }
            return new MsTrans(this);
        }

        public void Commit()
        {
            DbTrans.Commit();
        }

        public int TransCount { get; set; }

        public void FreeTrans()
        {
            if (DbTrans != null)
            {
                var trans = DbTrans;
                DbTrans = null;
                trans.Dispose();
            }
        }

        public string InsertIgnore => "INSERT IGNORE INTO";
        public string UtcTimestampFunction => "UTC_TIMESTAMP()";

        public void Lock(string tableName)
        {
            using (var cmd = new MySqlCommand($"LOCK TABLES {tableName} WRITE", DbConn))
                cmd.ExecuteNonQuery();
        }

        public void Unlock()
        {
            using (var cmd = new MySqlCommand("UNLOCK TABLES", DbConn))
                cmd.ExecuteNonQuery();
        }

        public int ExecuteSql(string sql, Dictionary<string, object> cmdParams = null)
        {
            int rowsAffected;
            using (var cmd = PrepCmd(sql, cmdParams))
                rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected;
        }

        public async Task<int> ExecuteSqlAsync(string sql, Dictionary<string, object> cmdParams = null)
        {
            int rowsAffected;
            using (var cmd = PrepCmd(sql, cmdParams))
                rowsAffected = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return rowsAffected;
        }

        public async Task<long> ExecuteInsertAsync(string sql, Dictionary<string, object> cmdParams = null)
        {
            using (var cmd = PrepCmd(sql, cmdParams))
            {
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                long id = cmd.LastInsertedId;
                return id;
            }
        }

        public async Task<object> ExecuteScalarAsync(string sql, Dictionary<string, object> cmdParams = null)
        {
            using (var cmd = PrepCmd(sql, cmdParams))
                return await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        }

        public async Task<DbDataReader> ExecuteReaderAsync(string sql, Dictionary<string, object> cmdParams = null)
        {
            using (var cmd = PrepCmd(sql, cmdParams))
                return await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        }

        private MySqlCommand PrepCmd(string sql, Dictionary<string, object> cmdParams = null)
        {
            var cmd = new MySqlCommand(sql, DbConn, DbTrans);
            if (cmdParams != null)
            {
                foreach (var kvp in cmdParams)
                    cmd.Parameters.AddWithValue(kvp.Key, kvp.Value);
            }
            return cmd;
        }

        private MySqlConnection DbConn;
        private MySqlTransaction DbTrans;
    }
}
