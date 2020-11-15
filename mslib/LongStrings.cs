using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace metastrings
{
    public static class LongStrings
    {
        public static async Task StoreStringAsync(Context ctxt, long itemId, string name, string longstring)
        {
            if (longstring.Length >= 64 * 1024)
                throw new MetaStringsException("String length exceeds max 64KB");

            var cmdParams =
                new Dictionary<string, object>
                {
                    { "@itemid", itemId },
                    { "@name", name },
                    { "@longstring", longstring }
                };
            string updateSql =
                "UPDATE longstrings SET longstring = @longstring WHERE itemid = @itemid AND name = @name";
            int affected = await ctxt.Db.ExecuteSqlAsync(updateSql, cmdParams).ConfigureAwait(false);
            if (affected == 0)
            {
                string insertSql =
                    "INSERT INTO longstrings (itemid, name, longstring)\n" +
                    "VALUES (@itemid, @name, @longstring)";
                await ctxt.Db.ExecuteSqlAsync(insertSql, cmdParams).ConfigureAwait(false);
            }
        }

        public static async Task<string> GetStringAsync(Context ctxt, long itemId, string name, string like = "")
        {
            var cmdParams =
                new Dictionary<string, object>
                {
                    { "@itemid", itemId },
                    { "@name", name },
                    { "@like", like }
                };
            string storeSql = "SELECT longstring FROM longstrings WHERE itemid= @itemid AND name = @name";
            if (!string.IsNullOrWhiteSpace(like))
                storeSql += " AND longstring LIKE @like";
            using (var reader = await ctxt.Db.ExecuteReaderAsync(storeSql, cmdParams).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync().ConfigureAwait(false))
                    return null;
                else
                    return reader.GetString(0);
            }
        }

        public static async Task DeleteStringAsync(Context ctxt, long itemId, string name)
        {
            var cmdParams =
                new Dictionary<string, object>
                {
                    { "@itemid", itemId },
                    { "@name", name },
                };
            string deleteSql = "DELETE FROM longstrings WHERE itemid = @itemid AND name = @name";
            await ctxt.Db.ExecuteSqlAsync(deleteSql, cmdParams).ConfigureAwait(false);
        }
    }
}
