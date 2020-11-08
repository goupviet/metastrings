using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace metastrings
{
    public static class Items
    {
        public static void Reset(Context ctxt)
        {
            ctxt.Db.ExecuteSql("DELETE FROM items");
        }

        public static async Task<long> GetIdAsync(Context ctxt, int tableId, long valueId, bool noCreate = false)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                Exception lastExp = null;
                for (int tryCount = 1; tryCount <= 4; ++tryCount)
                {
                    try
                    {
                        Dictionary<string, object> cmdParams =
                            new Dictionary<string, object>
                            {
                                { "@tableId", tableId },
                                { "@valueId", valueId }
                            };
                        string selectSql = "SELECT id FROM items WHERE tableId = @tableId AND valueId = @valueId";
                        object idObj = await ctxt.Db.ExecuteScalarAsync(selectSql, cmdParams).ConfigureAwait(false);
                        long id = Utils.ConvertDbInt64(idObj);
                        if (noCreate && id < 0)
                            return -1;
                        else if (id >= 0)
                            return id;

                        string insertSql = $"{ctxt.Db.InsertIgnore} items (tableid, valueid, created, lastmodified) VALUES (@tableId, @valueId, {ctxt.Db.UtcTimestampFunction}, {ctxt.Db.UtcTimestampFunction})";
                        id = await ctxt.Db.ExecuteInsertAsync(insertSql, cmdParams).ConfigureAwait(false);
                        return id;
                    }
                    catch (Exception exp)
                    {
                        lastExp = exp;
                    }
                }

                throw new MetaStringsException("Items.GetId failed after a few tries", lastExp);
            }
            finally
            {
                ScopeTiming.RecordScope("Items.GetId", totalTimer);
            }
        }

        public static async Task<Dictionary<int, long>> GetItemDataAsync(Context ctxt, long itemId)
        {
            var retVal = new Dictionary<int, long>();
            string sql = $"SELECT nameid, valueid FROM itemnamevalues WHERE itemid = {itemId}";
            using (var reader = await ctxt.Db.ExecuteReaderAsync(sql).ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                    retVal[reader.GetInt32(0)] = reader.GetInt64(1);
            }
            return retVal;
        }

        public static void SetItemData(Context ctxt, long itemId, Dictionary<int, long> itemData)
        {
            string updateSql = $"UPDATE items SET lastmodified = {ctxt.Db.UtcTimestampFunction} WHERE id = {itemId}";
            ctxt.AddPostOp(updateSql);

            foreach (var kvp in itemData)
            {
                string sql;
                if (kvp.Value >= 0) // add-or-update it
                {
                    sql =
                        "INSERT INTO itemnamevalues (itemid, nameid, valueid) " +
                        $"VALUES ({itemId}, {kvp.Key}, {kvp.Value})";

                    sql += " ";
                    
                    sql += $"ON DUPLICATE KEY UPDATE valueid = {kvp.Value}";
                }
                else // remove it
                    sql = $"DELETE FROM itemnamevalues WHERE itemid = {itemId} AND nameid = {kvp.Key}";
                ctxt.AddPostOp(sql);
            }
        }

        public static async Task DeleteAsync(Context ctxt, long itemId)
        {
            string sql = $"DELETE FROM items WHERE id = {itemId}";
            await ctxt.Db.ExecuteSqlAsync(sql).ConfigureAwait(false);
        }
    }
}
