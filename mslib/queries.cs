using System;
using System.Collections.Generic;

namespace metastrings
{
    public class Define
    {
        public string table { get; set; }

        // item value -> metadata name-> value
        public Dictionary<object, Dictionary<string, object>> metadata { get; set; }

        // helper function
        public void SetData(object itemValue, string metadataName, object metadataValue)
        {
            if (metadata == null)
                metadata = new Dictionary<object, Dictionary<string, object>>();

            if (m_namesAreNumeric == null)
                m_namesAreNumeric = new Dictionary<string, bool>();

            Dictionary<string, object> metadataDict;
            if (!metadata.TryGetValue(itemValue, out metadataDict))
            {
                metadataDict = new Dictionary<string, object>();
                metadata.Add(itemValue, metadataDict);
            }

            if (metadataValue != null)
            {
                bool isValueNumeric = !(metadataValue is string);
                if (!m_namesAreNumeric.ContainsKey(metadataName))
                {
                    m_namesAreNumeric.Add(metadataName, isValueNumeric);
                }
                else if (isValueNumeric != m_namesAreNumeric[metadataName])
                {
                    throw new MetaStringsException($"Setting metadata name {metadataName} value type mismatch" +
                                                   $"\n - Existing data numeric = {!isValueNumeric}" +
                                                   $"\n - Value: {metadataName}");
                }
            }

            metadataDict[metadataName] = metadataValue;
        }

        private Dictionary<string, bool> m_namesAreNumeric;
    }

    public class GetRequest
    {
        public string table { get; set; }
        public List<object> values { get; set; }
    }

    public class Criteria // WHERE
    {
        public string name { get; set; }
        public string op { get; set; }
        public string paramName { get; set; }
    }

    public enum CriteriaCombine { AND, OR }
    public class CriteriaSet
    {
        public CriteriaCombine combine { get; set; } = CriteriaCombine.AND;
        public List<Criteria> criteria { get; set; } = new List<Criteria>();

        public CriteriaSet() { }

        public CriteriaSet(Criteria criteria)
        {
            AddCriteria(criteria);
        }

        public static List<CriteriaSet> GenWhere(Criteria criteria)
        {
            return new List<CriteriaSet>{ new CriteriaSet(criteria) };
        }

        public static IEnumerable<CriteriaSet> GenWhere(IEnumerable<Criteria> criteria)
        {
            CriteriaSet set = new CriteriaSet();
            foreach (var c in criteria)
                set.AddCriteria(c);
            return new[] { new CriteriaSet() };
        }

        public void AddCriteria(Criteria criteria)
        {
            this.criteria.Add(criteria);
        }
    }

    public class Order // ORDER BY
    {
        public string field { get; set; }
        public bool descending { get; set; }
    }

    public class QueryGetRequest
    {
        public string from { get; set; } // FROM
        public List<CriteriaSet> where { get; set; }
        public List<Order> orderBy { get; set; }
        public int limit { get; set; }

        public Dictionary<string, object> cmdParams { get; set; }

        public void AddParam(string name, object value)
        {
            if (cmdParams == null)
                cmdParams = new Dictionary<string, object>();
            cmdParams.Add(name, value);
        }

        public void AddOrder(string name, bool descending)
        {
            if (orderBy == null)
                orderBy = new List<Order>();
            orderBy.Add(new Order() { field = name, descending = descending });
        }
    }

    public class Select : QueryGetRequest
    {
        public List<string> select { get; set; }
    }

    public class GetResponse
    {
        public List<Dictionary<string, object>> metadata { get; set; }
    }

    public class Delete
    {
        public string table { get; set; }
        public List<object> values { get; set; }
    }

    public class Drop
    {
        public string table { get; set; }
    }

    public class Reset
    {
        public bool includeNameValues { get; set; }
    }

    public class Timing
    {
        public bool reset { get; set; }
    }

    public class Schema
    {
        public string table { get; set; } // optional to get the full schema
    }

    public class SchemaResponse
    {
        public ListDictionary<string, List<string>> tables { get; set; }
    }

    public class TableCreate
    {
        public string table { get; set; }
        public bool isNumeric { get; set; }
    }

    public class LongStringOp
    {
        public string table { get; set; }
        public object itemValue { get; set; }
        public string fieldName { get; set; }
    }

    public class LongStringPut : LongStringOp
    {
        public string longString { get; set; }
    }

    public class LongStringQuery
    {
        public string table { get; set; }
        public string fieldName { get; set; }
        public string query { get; set; }
    }
}
