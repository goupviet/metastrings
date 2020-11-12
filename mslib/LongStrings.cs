using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace metastrings
{
    public static class LongStrings
    {
        public static async Task StoreStringAsync(Context ctxt, long itemId, string name, string longstring)
        {
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
            if (affected == 0) // unlikely
            {
                string insertSql =
                    "INSERT INTO longstrings (itemid, name, longstring)\n" +
                    "VALUES (@itemid, @name, @longstring)";
                await ctxt.Db.ExecuteSqlAsync(insertSql, cmdParams).ConfigureAwait(false);
            }
        }

        public static async Task<string> GetStringAsync(Context ctxt, long itemId, string name)
        {
            var cmdParams =
                new Dictionary<string, object>
                {
                    { "@itemid", itemId },
                    { "@name", name }
                };
            string storeSql = "SELECT longstring FROM longstrings WHERE itemid= @itemid AND name = @name";
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

        public static async Task<List<object>> QueryStringsAsync(Context ctxt, int tableId, string name, string query)
        {
            string match = "MATCH(longstring) AGAINST (@query IN BOOLEAN MODE)";
            string querySql = 
                $"SELECT items.valueid, {match} AS relevance\n" +
                $"FROM longstrings\n" +
                $"JOIN items ON items.id = longstrings.itemid\n" +
                $"WHERE longstrings.name = @name AND {match}\n" +
                $"AND items.tableid = {tableId}\n" +
                $"ORDER BY relevance DESC";
            var cmdParams =
                new Dictionary<string, object>
                {
                    { "@name", name },
                    { "@query", query }
                };
            var valueIds = new List<long>();
            using (var reader = await ctxt.Db.ExecuteReaderAsync(querySql, cmdParams).ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                    valueIds.Add(reader.GetInt64(0));
            }

            var values = new List<object>();
            foreach (var valueId in valueIds)
                values.Add(await Values.GetValueAsync(ctxt, valueId).ConfigureAwait(false));
            
            return values;
        }
    }
}
