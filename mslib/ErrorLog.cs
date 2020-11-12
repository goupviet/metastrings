using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace metastrings
{
    public class ErrorLogEntry
    {
        public long userId { get; set; }
        public string ip { get; set; }
        public DateTime when { get; set; }
        public string msg { get; set; }
    }

    public static class ErrorLog
    {
        public static void Log(Context ctxt, long userId, string ip, string msg)
        {
            string sql = "INSERT INTO errorlog (userid, ip, msg, logdate) VALUES (@userId, @ip, @msg, @when)";
            var cmdParams = new Dictionary<string, object>();
            cmdParams.Add("@userId", userId);
            cmdParams.Add("@ip", ip);
            cmdParams.Add("@when", DateTime.UtcNow);
            cmdParams.Add("@msg", msg);
            ctxt.Db.ExecuteSql(sql, cmdParams);
        }

        public static async Task<List<ErrorLogEntry>> QueryAsync(Context ctxt, string likePattern, int maxDaysOld)
        {
            var output = new List<ErrorLogEntry>();
            string sql = "SELECT userid, ip, logdate, msg FROM errorlog WHERE msg LIKE @like AND logdate > @since ORDER BY logdate DESC";
            var cmdParams = new Dictionary<string, object>();
            cmdParams.Add("@like", likePattern);
            cmdParams.Add("@since", DateTime.UtcNow - TimeSpan.FromDays(maxDaysOld));
            using (var reader = await ctxt.Db.ExecuteReaderAsync(sql, cmdParams))
            {
                while (await reader.ReadAsync())
                {
                    output.Add
                    (
                        new ErrorLogEntry()
                        {
                            userId = reader.GetInt64(0),
                            ip = reader.GetString(1),
                            when = reader.GetDateTime(2),
                            msg = reader.GetString(3)
                        }
                    );
                }
            }
            return output;
        }

        public static void Clear(Context ctxt)
        {
            ctxt.Db.ExecuteSql("TRUNCATE TABLE errorlog");
        }
    }
}
