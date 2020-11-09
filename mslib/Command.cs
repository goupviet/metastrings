using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.IO;

namespace metastrings
{
    public class Command : IDisposable
    {
        public Command(Context ctxt, bool keepCtxtOpen = true)
        {
            Ctxt = ctxt;
            m_keepCtxtOpen = keepCtxtOpen;
        }

        public void Dispose()
        {
            if (Ctxt != null && !m_keepCtxtOpen)
            {
                Ctxt.Dispose();
                Ctxt = null;
            }
        }

        public async Task DefineAsync(Define define)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                var localTimer = ScopeTiming.StartTiming();

                if (define.metadata == null || define.metadata.Count == 0)
                    return;

                using (var msTrans = Ctxt.BeginTrans())
                {
                    // values are the primary keys of the items were UPSERT'ing
                    var values = new List<object>(define.metadata.Count);
                    values.AddRange(define.metadata.Keys); 

                    bool firstValueIsNumeric = !(values[0] is string);
                    int tableId = await Tables.GetIdAsync(Ctxt, define.table, firstValueIsNumeric).ConfigureAwait(false);
                    TableObj table = await Tables.GetTableAsync(Ctxt, tableId).ConfigureAwait(false);

                    // Check that all primary key values match the table's isnumeric
                    foreach (object value in values)
                    {
                        bool valueIsNumeric = !(value is string);
                        if (table.isNumeric != valueIsNumeric)
                            throw
                                new MetaStringsException
                                (
                                    $"Value numeric does not match table configuration: {define.table}" +
                                    $"\n - table numeric: {table.isNumeric}" +
                                    $"\n - value numeric: {valueIsNumeric} -  {value}"
                                );
                    }
                    ScopeTiming.RecordScope("Define.Setup", localTimer);

                    // value => valueid
                    var valueIdCache = new Dictionary<object, long>(values.Count); 
                    foreach (object value in values)
                        valueIdCache[value] = await Values.GetIdAsync(Ctxt, value).ConfigureAwait(false);
                    ScopeTiming.RecordScope("Define.Values", localTimer);

                    // value => itemid
                    var valueIdsItemIdCache = new Dictionary<object, long>(values.Count);
                    foreach (object value in values)
                    {
                        long itemId = await Items.GetIdAsync(Ctxt, tableId, valueIdCache[value]).ConfigureAwait(false);
                        valueIdsItemIdCache[value] = itemId;
                    }
                    ScopeTiming.RecordScope("Define.Items", localTimer);

                    // name => nameid
                    var nameIds = new Dictionary<string, int>();
                    foreach (object value in values)
                    {
                        var metadata = define.metadata[value];
                        if (metadata == null || metadata.Count == 0)
                            continue;

                        foreach (var kvp in metadata)
                        {
                            int nameId;
                            if (!nameIds.TryGetValue(kvp.Key, out nameId))
                            {
                                bool isValueNumeric = !(kvp.Value is string);
                                nameId = await Names.GetIdAsync(Ctxt, tableId, kvp.Key, isValueNumeric).ConfigureAwait(false);
                                nameIds.Add(kvp.Key, nameId);
                            }
                        }
                    }
                    ScopeTiming.RecordScope("Define.NameIds", localTimer);

                    // nameid => nameobj
                    var nameObjs = new Dictionary<int, NameObj>();
                    foreach (int nameId in nameIds.Values)
                    {
                        NameObj nameObj;
                        if (!nameObjs.TryGetValue(nameId, out nameObj))
                        {
                            nameObj = await Names.GetNameAsync(Ctxt, nameId).ConfigureAwait(false);
                            nameObjs.Add(nameId, nameObj);
                        }
                    }
                    ScopeTiming.RecordScope("Define.NameObjs", localTimer);

                    foreach (object value in values)
                    {
                        var metadata = define.metadata[value];
                        if (metadata == null || metadata.Count == 0)
                            continue;

                        // nameid => valueid
                        var nameValueIds = new Dictionary<int, long>();
                        foreach (var kvp in metadata)
                        {
                            int nameId = nameIds[kvp.Key];
                            if (kvp.Value == null) // erase value
                            {
                                nameValueIds[nameId] = -1;
                                continue;
                            }

                            // Type safety
                            var nameObj = nameObjs[nameId];
                            bool isValueNumeric = !(kvp.Value is string);
                            if (isValueNumeric != nameObj.isNumeric)
                            {
                                throw
                                    new MetaStringsException
                                    (
                                        $"Data numeric does not match name: {nameObj.name}" +
                                        $"\n - value is numeric: {isValueNumeric} - {kvp.Value}" +
                                        $"\n -  name is numeric: {nameObj.isNumeric}"
                                    );
                            }

                            long valueId;
                            if (!valueIdCache.TryGetValue(kvp.Value, out valueId))
                            {
                                valueId = await Values.GetIdAsync(Ctxt, kvp.Value).ConfigureAwait(false);
                                valueIdCache[kvp.Value] = valueId;
                            }

                            nameValueIds[nameId] = valueId;
                        }

                        Items.SetItemData(Ctxt, valueIdsItemIdCache[value], nameValueIds);
                    }
                    msTrans.Commit();
                    ScopeTiming.RecordScope("Define.Metadata", localTimer);
                }

                await Ctxt.ProcessPostOpsAsync().ConfigureAwait(false);
                ScopeTiming.RecordScope("Define.PostOps", localTimer);
            }
#if !DEBUG
            catch
            {
                Ctxt.ClearPostOps();
                NameValues.ClearCaches();
                throw;
            }
#endif
            finally
            {
                ScopeTiming.RecordScope("Define", totalTimer);
            }
        }

        public async Task<string> GenerateSqlAsync(Select query)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                string sql = await Sql.GenerateSqlAsync(Ctxt, query).ConfigureAwait(false);
                return sql;
            }
            finally
            {
                ScopeTiming.RecordScope("Cmd.GenerateSql", totalTimer);
            }
        }

        public async Task<Reader> GetReaderAsync(Select query)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                string sql = await GenerateSqlAsync(query).ConfigureAwait(false);
                return await GetReaderAsync(sql, query.cmdParams).ConfigureAwait(false);
            }
            finally
            {
                ScopeTiming.RecordScope("Cmd.GenerateSql(query)", totalTimer);
            }
        }

        public async Task<Reader> GetReaderAsync(string sql, Dictionary<string, object> cmdParams)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                return new Reader(await Ctxt.Db.ExecuteReaderAsync(sql, cmdParams));
            }
            finally
            {
                ScopeTiming.RecordScope("Cmd.GenerateSql(sql)", totalTimer);
            }
        }

        public async Task<GetResponse> GetAsync(GetRequest request)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                var responses = new List<Dictionary<string, object>>(request.values.Count);

                int tableId = await Tables.GetIdAsync(Ctxt, request.table, noCreate: true).ConfigureAwait(false);
                foreach (var value in request.values)
                {
                    long valueId = await Values.GetIdAsync(Ctxt, value).ConfigureAwait(false);

                    long itemId = await Items.GetIdAsync(Ctxt, tableId, valueId, noCreate: true).ConfigureAwait(false);
                    if (itemId < 0)
                    {
                        responses.Add(null);
                        continue;
                    }

                    var metaIds = await Items.GetItemDataAsync(Ctxt, itemId).ConfigureAwait(false);
                    var metaStrings = await NameValues.GetMetadataValuesAsync(Ctxt, metaIds).ConfigureAwait(false);

                    responses.Add(metaStrings);
                }

                GetResponse response = new GetResponse() { metadata = responses };
                return response;
            }
            finally
            {
                ScopeTiming.RecordScope("Cmd.Get", totalTimer);
            }
        }

        public async Task<GetResponse> QueryGetAsync(QueryGetRequest request)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                var itemValues = new Dictionary<long, object>();
                {
                    Select select = new Select();
                    select.select = new List<string> { "id", "value" };
                    select.from = request.from;
                    select.where = request.where;
                    select.orderBy = request.orderBy;
                    select.limit = request.limit;
                    select.cmdParams = request.cmdParams;
                    using (var reader = await GetReaderAsync(select).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                            itemValues.Add(reader.GetInt64(0), reader.GetObject(1));
                    }
                }

                var responses = new List<Dictionary<string, object>>(itemValues.Count);
                foreach (var itemId in itemValues.Keys)
                {
                    var metaIds = await Items.GetItemDataAsync(Ctxt, itemId).ConfigureAwait(false);
                    var metaStrings = await NameValues.GetMetadataValuesAsync(Ctxt, metaIds).ConfigureAwait(false);
                    
                    metaStrings["id"] = (double)itemId;
                    metaStrings["value"] = itemValues[itemId];
                    
                    responses.Add(metaStrings);
                }

                GetResponse response = new GetResponse() { metadata = responses };
                return response;
            }
            finally
            {
                ScopeTiming.RecordScope("Cmd.QueryGet", totalTimer);
            }
        }

        public async Task DeleteAsync(string table, object id)
        {
            await DeleteAsync(table, new[] { id }).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string table, IEnumerable<object> ids)
        {
            await DeleteAsync(new Delete() { table = table, values = new List<object>(ids) }).ConfigureAwait(false);
        }

        public async Task DeleteAsync(Delete toDelete)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                int tableId = await Tables.GetIdAsync(Ctxt, toDelete.table, noCreate: true, noException: true).ConfigureAwait(false);
                if (tableId < 0)
                    return;

                foreach (var val in toDelete.values)
                {
                    long valueId = await Values.GetIdAsync(Ctxt, val).ConfigureAwait(false);
                    string sql = $"DELETE FROM items WHERE valueid = {valueId} AND tableid = {tableId}";
                    Ctxt.AddPostOp(sql);
                }

                await Ctxt.ProcessPostOpsAsync().ConfigureAwait(false);
            }
            finally
            {
                ScopeTiming.RecordScope("Cmd.Delete", totalTimer);
            }
        }

        public async Task DropAsync(Drop drop)
        {
            var totalTimer = ScopeTiming.StartTiming();
            try
            {
                int tableId = await Tables.GetIdAsync(Ctxt, drop.table, noCreate: true).ConfigureAwait(false);
                await Ctxt.Db.ExecuteSqlAsync($"DELETE FROM itemnamevalues WHERE nameid IN (SELECT id FROM names WHERE tableid = {tableId})").ConfigureAwait(false);
                await Ctxt.Db.ExecuteSqlAsync($"DELETE FROM names WHERE tableid = {tableId}").ConfigureAwait(false);
                await Ctxt.Db.ExecuteSqlAsync($"DELETE FROM items WHERE tableid = {tableId}").ConfigureAwait(false);
                await Ctxt.Db.ExecuteSqlAsync($"DELETE FROM tables WHERE id = {tableId}").ConfigureAwait(false);
            }
            finally
            {
                ScopeTiming.RecordScope("Cmd.Drop", totalTimer);
            }
        }

        public void Reset(Reset reset)
        {
            if (reset.includeNameValues)
                NameValues.Reset(Ctxt);
            else
                Items.Reset(Ctxt);

            NameValues.ClearCaches();
        }

        public async Task<SchemaResponse> GetSchemaAsync(Schema schema)
        {
            string sql =
                "SELECT t.name AS tablename, n.name AS colname " +
                "FROM tables t JOIN names n ON n.tableid = t.id";

            string requestedTable = schema.table;
            bool haveRequestedTableName = !string.IsNullOrWhiteSpace(requestedTable);
            if (haveRequestedTableName)
                sql += " WHERE t.name = @name";
            
            sql += " ORDER BY tablename, colname";

            Dictionary<string, object> cmdParams = new Dictionary<string, object>();
            if (haveRequestedTableName)
                cmdParams.Add("@name", requestedTable);

            var responseDict = new ListDictionary<string, List<string>>();
            using (var reader = await Ctxt.Db.ExecuteReaderAsync(sql, cmdParams).ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    string table = reader.GetString(0);
                    string colname = reader.GetString(1);

                    if (!responseDict.ContainsKey(table))
                        responseDict.Add(table, new List<string>());

                    responseDict[table].Add(colname);
                }
            }

            SchemaResponse response = new SchemaResponse() { tables = responseDict };
            return response;
        }

        public async Task CreateTableAsync(TableCreate create)
        {
            await Tables.GetIdAsync(Ctxt, create.table, create.isNumeric).ConfigureAwait(false);
        }

        public async Task PutLongStringAsync(LongStringPut put)
        {
            long itemId = await GetLongStringItemIdAsync(put).ConfigureAwait(false);
            await LongStrings.StoreStringAsync(Ctxt, itemId, put.fieldName, put.longString).ConfigureAwait(false);
        }

        public async Task<string> GetLongStringAsync(LongStringOp get)
        {
            long itemId = await GetLongStringItemIdAsync(get).ConfigureAwait(false);
            string longString = await LongStrings.GetStringAsync(Ctxt, itemId, get.fieldName).ConfigureAwait(false);
            return longString;
        }

        public async Task DeleteLongStringAsync(LongStringOp del)
        {
            long itemId = await GetLongStringItemIdAsync(del).ConfigureAwait(false);
            await LongStrings.DeleteStringAsync(Ctxt, itemId, del.fieldName).ConfigureAwait(false);
        }

        public async Task<List<object>> QueryLongStringsAsync(LongStringQuery query)
        {
            int tableId = await Tables.GetIdAsync(Ctxt, query.table, noCreate: true).ConfigureAwait(false);
            var results = await LongStrings.QueryStringsAsync(Ctxt, tableId, query.fieldName, query.query).ConfigureAwait(false);
            return results;
        }

        private async Task<long> GetLongStringItemIdAsync(LongStringOp op)
        {
            int tableId = await Tables.GetIdAsync(Ctxt, op.table, noCreate: true).ConfigureAwait(false);
            long valueId = await Values.GetIdAsync(Ctxt, op.itemValue).ConfigureAwait(false);
            long itemId = await Items.GetIdAsync(Ctxt, tableId, valueId, noCreate: true).ConfigureAwait(false);
            return itemId;
        }

        private Context Ctxt;
        private bool m_keepCtxtOpen;
    }
}
