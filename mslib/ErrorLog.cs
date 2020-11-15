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
        public static async Task LogAsync(Context ctxt, long userId, string ip, string msg)
        {
            string logKey = Guid.NewGuid().ToString();
            Define define = new Define() { table = "errorlog", key = logKey };
            define.SetData("userid", userId);
            define.SetData("ip", ip);
            define.SetData("when", DateTime.UtcNow.ToString("o"));
            await ctxt.Cmd.DefineAsync(define).ConfigureAwait(false);
            long logId = await ctxt.GetRowIdAsync("errorlog", logKey);
            await ctxt.Cmd.PutLongStringAsync
            (
                new LongStringPut()
                {
                    table = "errorlog",
                    fieldName = "msg",
                    itemId = logId,
                    longString = msg
                }
            );
        }

        public static async Task<List<ErrorLogEntry>> QueryAsync(Context ctxt, string likePattern, int maxDaysOld)
        {
            var output = new List<ErrorLogEntry>();
            string sql = "SELECT id, created FROM errorlog WHERE created > @since ORDER BY created DESC";
            var select = Sql.Parse(sql);
            select.AddParam("@since", DateTime.UtcNow - TimeSpan.FromDays(maxDaysOld));
            List<long> itemIds = await ctxt.ExecListAsync<long>(select);
            foreach (long itemId in itemIds)
            {
                string logMessage = await LongStrings.GetStringAsync(ctxt, itemId, "msg", likePattern);
                if (string.IsNullOrWhiteSpace(logMessage))
                    continue;

                var entrySelect = Sql.Parse("SELECT userid, ip, created FROM errorlog WHERE id = @id");
                entrySelect.AddParam("@id", itemId);
                using (var reader = await ctxt.ExecSelectAsync(entrySelect))
                {
                    while (await reader.ReadAsync())
                    {
                        var newEntry = new ErrorLogEntry()
                        {
                            userId = reader.GetInt64(0),
                            ip = reader.GetString(1),
                            when = reader.GetDateTime(2),
                            msg = logMessage
                        };
                        output.Add(newEntry);
                    }
                }
            }
            return output;
        }

        public static async Task ClearAsync(Context ctxt)
        {
            await ctxt.Cmd.DropAsync(new Drop() { table = "errorlog" });
        }
    }
}
