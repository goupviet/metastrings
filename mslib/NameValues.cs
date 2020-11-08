using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace metastrings
{
    public static class NameValues
    {
        public static void Reset(Context ctxt)
        {
            Items.Reset(ctxt);

            Values.Reset(ctxt);
            Names.Reset(ctxt);
            Tables.Reset(ctxt);
        }

        public static void ClearCaches()
        {
            Values.ClearCaches();
            Names.ClearCaches();
            Tables.ClearCaches();
        }

        public static async Task<Dictionary<string, object>> GetMetadataValuesAsync(Context ctxt, Dictionary<int, long> ids)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                var retVal = new Dictionary<string, object>(ids.Count);
                if (ids.Count == 0)
                    return retVal;

                await Names.CacheNamesAsync(ctxt, ids.Keys).ConfigureAwait(false);
                await Values.CacheValuesAsync(ctxt, ids.Values).ConfigureAwait(false);

                foreach (var kvp in ids)
                {
                    NameObj name = await Names.GetNameAsync(ctxt, kvp.Key).ConfigureAwait(false);
                    object value = await Values.GetValueAsync(ctxt, kvp.Value).ConfigureAwait(false);
                    retVal.Add(name.name, value);
                }
                return retVal;
            }
            finally
            {
                ScopeTiming.RecordScope("NameValues.GetMetadataValuesAsync", totalTimer);
            }
        }
    }
}
