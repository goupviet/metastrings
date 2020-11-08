using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.Common;
using System.Data;

namespace metastrings
{
    public interface IDb : IDisposable
    {
        MsTrans BeginTrans(IsolationLevel level = IsolationLevel.Unspecified);
        void Commit();
        int TransCount { get; set; }
        void FreeTrans();

        string InsertIgnore { get; }
        string UtcTimestampFunction { get; }

        void Lock(string tableName);
        void Unlock();

        int ExecuteSql(string sql, Dictionary<string, object> cmdParams = null);
        Task<int> ExecuteSqlAsync(string sql, Dictionary<string, object> cmdParams = null);
        Task<long> ExecuteInsertAsync(string sql, Dictionary<string, object> cmdParams = null);
        Task<object> ExecuteScalarAsync(string sql, Dictionary<string, object> cmdParams = null);
        Task<DbDataReader> ExecuteReaderAsync(string sql, Dictionary<string, object> cmdParams = null);
    }
}
