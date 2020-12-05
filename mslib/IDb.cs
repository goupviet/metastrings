using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.Common;
using System.Data;

namespace metastrings
{
    /// <summary>
    /// Generic database type
    /// Was useful when metastrings worked with MySQL and SqLite
    /// Left here in case another engine swaps MySQL out, like Posgres
    /// </summary>
    public interface IDb : IDisposable
    {
        MsTrans BeginTrans(IsolationLevel level = IsolationLevel.Unspecified);
        void Commit();
        int TransCount { get; set; }
        void FreeTrans();

        string InsertIgnore { get; }
        string UtcTimestampFunction { get; }

        int ExecuteSql(string sql, Dictionary<string, object> cmdParams = null);
        Task<int> ExecuteSqlAsync(string sql, Dictionary<string, object> cmdParams = null);
        Task<long> ExecuteInsertAsync(string sql, Dictionary<string, object> cmdParams = null);
        Task<object> ExecuteScalarAsync(string sql, Dictionary<string, object> cmdParams = null);
        Task<DbDataReader> ExecuteReaderAsync(string sql, Dictionary<string, object> cmdParams = null);
    }
}
