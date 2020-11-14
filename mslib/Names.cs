using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace metastrings
{
    public class NameObj
    {
        public int id;
        public int tableId;
        public string name;
        public bool isNumeric;
    }

    public static class Names
    {
        public static void Reset(Context ctxt)
        {
            sm_cache.Clear();
            sm_cacheBack.Clear();

            ctxt.Db.ExecuteSql("DELETE FROM names");
        }

        public static async Task<int> GetIdAsync(Context ctxt, int tableId, string name, bool isNumeric = false, bool noCreate = false, bool noException = false)
        {
            int id;
            string cacheKey = $"{tableId}_{name}";
            if (sm_cache.TryGetValue(cacheKey, out id))
                return id;

            if (!Utils.IsWord(name))
                throw new MetaStringsException($"Names.GetId name is not valid: {name} - {Utils.WordPattern}");

            if (Utils.IsNameReserved(name))
                throw new MetaStringsException($"Names.GetId name is reserved: {name} - {string.Join(", ", Utils.ReservedWords)}");

            Exception lastExp = null;
            bool isExpFinal = false;
            for (int tryCount = 1; tryCount <= 4; ++tryCount)
            {
                try
                {
                    Dictionary<string, object> cmdParams = new Dictionary<string, object>();
                    cmdParams.Add("@tableId", tableId);
                    cmdParams.Add("@name", name);
                    string selectSql =
                        "SELECT id FROM names WHERE tableid = @tableId AND name = @name";
                    object idObj = await ctxt.Db.ExecuteScalarAsync(selectSql, cmdParams).ConfigureAwait(false);
                    id = Utils.ConvertDbInt32(idObj);
                    if (id >= 0)
                    {
                        sm_cache[cacheKey] = id;
                        return id;
                    }

                    if (noCreate)
                    {
                        if (noException)
                            return -1;

                        isExpFinal = true;
                        throw new MetaStringsException($"Names.GetId cannot create new name: {name}", lastExp);
                    }

	                cmdParams.Add("@isNumeric", isNumeric);
	                string insertSql =
	                    "INSERT INTO names (tableid, name, isNumeric) VALUES (@tableId, @name, @isNumeric)";
	                id = (int)await ctxt.Db.ExecuteInsertAsync(insertSql, cmdParams).ConfigureAwait(false);
	                if (id >= 0)
	                {
	                    sm_cache[cacheKey] = id;
	                    return id;
	                }
                }
                catch (Exception exp)
                {
                    if (isExpFinal)
                        throw exp;

                    lastExp = exp;
                }
            }

            throw new MetaStringsException("Names.GetId fails after a few tries", lastExp);
        }

        public static async Task<NameObj> GetNameAsync(Context ctxt, int id)
        {
            if (id < 0)
                return null;

            NameObj obj;
            if (sm_cacheBack.TryGetValue(id, out obj))
                return obj;

            string sql = $"SELECT tableid, name, isNumeric FROM names WHERE id = {id}";
            using (var reader = await ctxt.Db.ExecuteReaderAsync(sql).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync().ConfigureAwait(false))
                    throw new MetaStringsException($"Names.GetName fails to find record: {id}");

                obj = new NameObj()
                {
                    id = id,
                    tableId = reader.GetInt32(0),
                    name = reader.GetString(1),
                    isNumeric = reader.GetBoolean(2)
                };
                sm_cacheBack[id] = obj;
                return obj;
            }
        }
        public static async Task<bool> GetNameIsNumericAsync(Context ctxt, int id)
        {
            if (id < 0)
                return false;

            bool isNumeric;
            if (sm_isNumericCache.TryGetValue(id, out isNumeric))
                return isNumeric;

            string sql = $"SELECT isNumeric FROM names WHERE id = {id}";
            object numericObj = await ctxt.Db.ExecuteScalarAsync(sql).ConfigureAwait(false);
            isNumeric = (numericObj == null || numericObj == DBNull.Value) ? false : (ulong)numericObj != 0;
            sm_isNumericCache[id] = isNumeric;
            return isNumeric;
        }

        public static async Task CacheNamesAsync(Context ctxt, IEnumerable<int> ids)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                var stillToGet = ids.Where(id => !sm_cacheBack.ContainsKey(id));
                if (!stillToGet.Any())
                    return;

                var nameIdInPart = string.Join(",", stillToGet.Select(i => i.ToString()));
                var sql = $"SELECT id, tableid, name, isNumeric FROM names WHERE id IN ({nameIdInPart})";
                if (sql.Length > 1000 * 1000)
                    throw new MetaStringsException("GetNames query exceeds SQL batch limit of 1M.  Use smaller batches of items.");

                using (var reader = await ctxt.Db.ExecuteReaderAsync(sql).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        int id = reader.GetInt32(0);
                        sm_cacheBack[id] = 
                            new NameObj() 
                            { 
                                id = id, 
                                tableId = reader.GetInt32(1), 
                                name = reader.GetString(2),
                                isNumeric = reader.GetBoolean(3)
                            };
                    }
                }
            }
            finally
            {
                ScopeTiming.RecordScope("Names.CacheNames", totalTimer);
            }
        }

        public static void ClearCaches()
        {
            sm_cache.Clear();
            sm_cacheBack.Clear();
            sm_isNumericCache.Clear();
        }

        private static ConcurrentDictionary<string, int> sm_cache = new ConcurrentDictionary<string, int>();
        private static ConcurrentDictionary<int, NameObj> sm_cacheBack = new ConcurrentDictionary<int, NameObj>();
        private static ConcurrentDictionary<int, bool> sm_isNumericCache = new ConcurrentDictionary<int, bool>();
    }
}
