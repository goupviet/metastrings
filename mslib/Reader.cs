using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.Common;

namespace metastrings
{
    public class Reader : IDisposable
    {
        public Reader(DbDataReader myReader)
        {
            m_myReader = myReader;
            m_fieldCount = m_myReader.FieldCount;
        }

        public void Dispose()
        {
            if (m_myReader != null)
            {
                m_myReader.Dispose();
                m_myReader = null;
            }
        }

        public async Task<bool> ReadAsync()
        {
            return await m_myReader.ReadAsync().ConfigureAwait(false);
        }

        public List<string> FieldNames
        {
            get
            {
                var names = new List<string>(m_fieldCount);
                for (int f = 0; f < m_fieldCount; ++f)
                    names.Add(m_myReader.GetName(f));
                return names;
            }
        }
        
        public int FieldCount { get { return m_fieldCount; } }

        public string GetString(int idx)
        {
            if (m_myReader.IsDBNull(idx))
                return "";

            var str = m_myReader.GetString(idx);
            return str;
        }

        public double GetNumber(int idx)
        {
            return m_myReader.GetDouble(idx);
        }

        public long GetInt64(int idx)
        {
            return (long)GetNumber(idx);
        }

        public bool IsNull(int idx)
        {
            return m_myReader.IsDBNull(idx);
        }

        public object GetObject(int idx)
        {
            return m_myReader.GetValue(idx);
        }

        private DbDataReader m_myReader;
        private int m_fieldCount;
    }
}
