using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace metastrings
{
    public class ErrorLogEntry
    {
        public DateTime when { get; set; }
        public string msg { get; set; }
    }

    public static class ErrorLog
    {
        public static void Log(Context ctxt, string msg)
        {
            string sql = "INSERT INTO errorlog (msg, logdate) VALUES (@msg, @when)";
            var cmdParams = new Dictionary<string, object>();
            cmdParams.Add("@msg", msg);
            cmdParams.Add("@when", DateTime.UtcNow);
            ctxt.Db.ExecuteSql(sql, cmdParams);
        }

        public static async Task<List<ErrorLogEntry>> QueryAsync(Context ctxt, string likePattern, int maxDaysOld)
        {
            var output = new List<ErrorLogEntry>();
            string sql = "SELECT logdate, msg FROM errorlog WHERE msg LIKE @like AND logdate > @since ORDER BY logdate DESC";
            var cmdParams = new Dictionary<string, object>();
            cmdParams.Add("@like", likePattern);
            cmdParams.Add("@since", DateTime.UtcNow - TimeSpan.FromDays(maxDaysOld));
            using (var reader = await ctxt.Db.ExecuteReaderAsync(sql, cmdParams))
            {
                while (await reader.ReadAsync())
                    output.Add(new ErrorLogEntry() { when = reader.GetDateTime(0), msg = reader.GetString(1) });
            }
            return output;
        }

        public static void Clear(Context ctxt)
        {
            ctxt.Db.ExecuteSql("TRUNCATE TABLE errorlog");
        }
    }
}
