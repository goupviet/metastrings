using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;

namespace metastrings
{
    public static class Values
    {
        public static void Reset(Context ctxt)
        {
            sm_cache.Clear();
            sm_cacheBack.Clear();

            ctxt.Db.ExecuteSql("DELETE FROM bvalues");
        }

        public static async Task<long> GetIdAsync(Context ctxt, object value)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                if (value == null)
                    return -1;

                if ((value is string) && value.ToString().Length > MaxStringLength)
                    throw new ArgumentException($"String length limit reached: {MaxStringLength}", "value");

                long id = -1;
                bool shouldCache = (value is string) || Convert.ToDouble(value) == Convert.ToInt64(value);
                if (shouldCache)
                {
                    if (sm_cache.TryGetValue(value, out id))
                        return id;
                }

                Exception lastExp = null;
                for (int tryCount = 1; tryCount <= 3; ++tryCount)
                {
                    try
                    {
                        id = await GetIdSelectAsync(ctxt, value).ConfigureAwait(false);
                        if (id >= 0)
                            break;

                        id = await GetIdInsertAsync(ctxt, value).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception exp)
                    {
                        lastExp = exp;
                    }
                }

                if (id >= 0)
                {
                    if (shouldCache)
                        sm_cache[value] = id;
                    return id;
                }

                throw new MetaStringsException("Values.GetId failed after a few retries", lastExp);
            }
            finally
            {
                ScopeTiming.RecordScope("Values.GetId", totalTimer);
            }
        }

        private static async Task<long> GetIdSelectAsync(Context ctxt, object value)
        {
            var localTimer = ScopeTiming.StartTiming();

            if (value is string)
            {
                string strValue = (string)value;
                Dictionary<string, object> cmdParams = new Dictionary<string, object>();
                cmdParams.Add("@stringValue", strValue);
                string selectSql =
                    "SELECT id FROM bvalues WHERE isNumeric = 0 AND stringValue = @stringValue";
                long id = Utils.ConvertDbInt64(await ctxt.Db.ExecuteScalarAsync(selectSql, cmdParams).ConfigureAwait(false));
                ScopeTiming.RecordScope($"Values.GetId.SELECT(string)", localTimer);
                return id;
            }
            else
            {
                double numberValue = Convert.ToDouble(value);
                Dictionary<string, object> cmdParams = new Dictionary<string, object>();
                cmdParams.Add("@numberValue", numberValue);
                string selectSql =
                    "SELECT id FROM bvalues WHERE isNumeric = 1 AND numberValue = @numberValue";
                long id = Utils.ConvertDbInt64(await ctxt.Db.ExecuteScalarAsync(selectSql, cmdParams).ConfigureAwait(false));
                ScopeTiming.RecordScope("Values.GetId.SELECT(number)", localTimer);
                return id;
            }
        }

        private static async Task<long> GetIdInsertAsync(Context ctxt, object value)
        {
            if (value is string)
            {
                string strValue = (string)value;
                Dictionary<string, object> cmdParams = new Dictionary<string, object>();
                cmdParams.Add("@stringValue", strValue);
                string insertSql =
                    "INSERT INTO bvalues (isNumeric, numberValue, stringValue) VALUES (0, 0.0, @stringValue)";
                long id = await ctxt.Db.ExecuteInsertAsync(insertSql, cmdParams).ConfigureAwait(false);
                return id;
            }
            else
            {
                double numberValue = Convert.ToDouble(value);
                Dictionary<string, object> cmdParams = new Dictionary<string, object>();
                cmdParams.Add("@numberValue", numberValue);
                string insertSql =
                    "INSERT INTO bvalues (isNumeric, numberValue, stringValue) VALUES (1, @numberValue, '')";
                long id = await ctxt.Db.ExecuteInsertAsync(insertSql, cmdParams).ConfigureAwait(false);
                return id;
            }
        }

        public static async Task<object> GetValueAsync(Context ctxt, long id)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                object objFromCache;
                if (sm_cacheBack.TryGetValue(id, out objFromCache))
                    return objFromCache;

                string sql = $"SELECT isNumeric, numberValue, stringValue FROM bvalues WHERE id = {id}";
                using (var reader = await ctxt.Db.ExecuteReaderAsync(sql).ConfigureAwait(false))
                { 
                    if (!await reader.ReadAsync().ConfigureAwait(false))
                        throw new MetaStringsException("Values.GetValue fails to find record with ID = " + id);
                    object toReturn;
                    bool isNumeric = reader.GetBoolean(0);
                    if (isNumeric)
                        toReturn = reader.GetDouble(1);
                    else
                        toReturn = reader.GetString(2);
                    sm_cacheBack[id] = toReturn;
                    return toReturn;
                }
            }
            finally
            {
                ScopeTiming.RecordScope("Values.GetValue", totalTimer);
            }
        }

        public static async Task CacheValuesAsync(Context ctxt, IEnumerable<long> ids)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                var stillToGet = ids.Where(id => !sm_cacheBack.ContainsKey(id));
                if (!stillToGet.Any())
                    return;

                var valueIdInPart = string.Join(",", stillToGet.Select(i => i.ToString()));
                var sql = $"SELECT id, isNumeric, numberValue, stringValue FROM bvalues WHERE id IN ({valueIdInPart})";
                if (sql.Length > 32 * 1024)
                    throw new MetaStringsException("GetValues query exceeds SQL batch limit of 1M.  Use a smaller batch of items.");

                using (var reader = await ctxt.Db.ExecuteReaderAsync(sql).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        long id = reader.GetInt64(0);

                        bool isNumeric = reader.GetBoolean(1);

                        object obj;
                        if (isNumeric)
                            obj = reader.GetDouble(2);
                        else
                            obj = reader.GetString(3);

                        sm_cacheBack[id] = obj;
                    }
                }
            }
            finally
            {
                ScopeTiming.RecordScope("Values.CacheValues", totalTimer);
            }
        }

        public static void ClearCaches()
        {
            sm_cache.Clear();
            sm_cacheBack.Clear();
        }

        public static int MaxStringLength = 255;

        private static ConcurrentDictionary<object, long> sm_cache = new ConcurrentDictionary<object, long>();
        private static ConcurrentDictionary<long, object> sm_cacheBack = new ConcurrentDictionary<long, object>();
    }
}
