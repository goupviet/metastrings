using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;

using Newtonsoft.Json;

namespace metastrings
{
    public static class Utils
    {
        public static string HashString(string input)
        {
            StringBuilder sb = new StringBuilder(64);
            using (var hasher = SHA256.Create())
            {
                byte[] hashBytes = hasher.ComputeHash(Encoding.Unicode.GetBytes(input));
                for (int i = 0; i < hashBytes.Length; i++)
                    sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public static T Deserialize<T>(string str)
        {
            using (TextReader txtReader = new StringReader(str))
            using (JsonReader jsReader = new JsonTextReader(txtReader))
                return sm_jsonSerializer.Deserialize<T>(jsReader);
        }

        public static string Serialize(object obj)
        {
            StringBuilder sb = new StringBuilder();
            using (TextWriter txtWriter = new StringWriter(sb))
                sm_jsonSerializer.Serialize(txtWriter, obj);
            return sb.ToString();
        }

        public static List<string> BatchUp(IEnumerable<string> pieces, int batchSize)
        {
            if (batchSize <= 0)
                throw new ArgumentException("batchSize must be > 0");

            var retVal = new List<string>();

            StringBuilder sb = new StringBuilder(batchSize);
            foreach (string str in pieces)
            {
                if (str.Length + sb.Length > batchSize)
                {
                    retVal.Add(sb.ToString());
                    sb.Clear();
                }

                sb.Append(str);
            }

            if (sb.Length > 0)
                retVal.Add(sb.ToString());

            return retVal;
        }

        public static List<List<T>> SplitUp<T>(IEnumerable<T> items, int batches)
        {
            if (batches == 0)
                batches = 1;

            if (batches <= 0)
                throw new ArgumentException("batches must be > 0");

            List<List<T>> retVal = new List<List<T>>(batches);

            if (batches == 1)
            {
                retVal.Add(new List<T>(items));
                return retVal;
            }

            int itemCount = items.Count();
            int batchSize = (int)Math.Round((double)itemCount / batches); // last batch can be fat

            for (int b = 0; b < batches; ++b)
            {
                var newList = new List<T>(batchSize);
                retVal.Add(newList);
            }

            int listIndex = 0;
            foreach (T t in items)
            {
                var curList = retVal[listIndex];
                curList.Add(t);

                if (curList.Count >= batchSize && listIndex != retVal.Count - 1)
                    ++listIndex;
            }

            return retVal;
        }

        public static Exception HandleException(Exception exp)
        {
            WebException webExp = null;
            if (exp is WebException)
                webExp = (WebException)exp;

            if (webExp == null || webExp.Response == null)
            {
                var innerExp = exp;
                while (innerExp.InnerException != null)
                    innerExp = innerExp.InnerException;

                string errorType = webExp == null ? "General" : "Network";
                return new MetaStringsException($"{errorType} Error: {innerExp.GetType().FullName}: {innerExp.Message}");
            }

            string responseString;
            using (var responseStream = webExp.Response.GetResponseStream())
            using (var sr = new StreamReader(responseStream, Encoding.UTF8))
                responseString = sr.ReadToEnd(); // can't do async in an exp handler

            return new MetaStringsException($"Server Error: {webExp.Status}: {responseString}");
        }

        public static long ConvertDbInt64(object obj)
        {
            if (obj == null || obj == DBNull.Value)
                return -1;
            else
                return Convert.ToInt64(obj);
        }

        public static int ConvertDbInt32(object obj)
        {
            if (obj == null || obj == DBNull.Value)
                return -1;
            else
                return Convert.ToInt32(obj);
        }

        public static List<string> BreakPathIntoWords(string path)
        {
            List<string> retVal = new List<string>();
            string[] parts = 
                path.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            string curWord = "";
            foreach (string part in parts)
            {
                foreach (char c in part)
                {
                    if (char.IsLetter(c))
                    {
                        curWord += c;
                    }
                    else
                    { 
                        if (curWord != "")
                            retVal.Add(curWord);
                        curWord = "";
                    }
                }

                if (curWord != "")
                    retVal.Add(curWord);
                curWord = "";
            }

            if (curWord != "")
                retVal.Add(curWord);
            return retVal;
        }

        public static string[] Tokenize(string sql)
        {
            string[] tokens = 
                sql.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens;
        }

        public static List<string> ExtractParamNames(string sql)
        {
            List<string> paramNames = new List<string>();
            StringBuilder sb = new StringBuilder();
            int lookFrom = 0;
            while (true)
            {
                if (lookFrom >= sql.Length)
                    break;

                int at = sql.IndexOf('@', lookFrom);
                if (at < 0)
                    break;

                sb.Clear();
                int idx = at + 1;
                while (idx < sql.Length)
                {
                    char c = sql[idx++];
                    if (char.IsLetterOrDigit(c) || c == '_')
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        break;
                    }
                }

                if (sb.Length > 0)
                {
                    paramNames.Add($"@{sb}");
                    sb.Clear();
                }
                lookFrom = idx;
            }

            if (sb.Length > 0)
            {
                paramNames.Add($"@{sb}");
                sb.Clear();
            }
            return paramNames;
        }

        public static string CleanName(string name) // used for table and column aliases
        {
            string clean = "";
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                    clean += c;
            }

            if (string.IsNullOrWhiteSpace(clean) || !char.IsLetter(clean[0]))
                clean = "a" + clean;

            return clean;
        }

        public static bool IsWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;
            else
                return IsWordRegEx.IsMatch(word) && !word.EndsWith("_", StringComparison.Ordinal);
        }

        public static string MakeSafeWord(string word)
        {
            return word.Replace(' ', '_');
        }

        public static bool IsParam(string param)
        {
            if (string.IsNullOrWhiteSpace(param))
                return false;
            else
                return IsParamRegEx.IsMatch(param) && !param.EndsWith("_", StringComparison.Ordinal);
        }

        public static bool IsNameReserved(string name)
        {
            return ReservedWords.Contains(name.ToLower());
        }

        public static void ValidateTableName(string table, string sql)
        {
            if (!Utils.IsWord(table))
                throw new SqlException($"Invalid table name: {table}", sql);
        }

        public static void ValidateColumnName(string col, string sql)
        {
            if (!Utils.IsWord(col))
                throw new SqlException($"Invalid column name: {col}", sql);
        }

        public static void ValidateParameterName(string parm, string sql)
        {
            if (!Utils.IsParam(parm))
                throw new SqlException($"Invalid parameter name: {parm}", sql);
        }

        public static void ValidateOperator(string op, string sql)
        {
            if (!QueryOps.Contains(op.ToLower()))
                throw new SqlException($"Invalid query operator: {op}", sql);
        }

        public const string WordPattern = "^[a-zA-Z](\\w)*$";
        private static Regex IsWordRegEx = new Regex(WordPattern, RegexOptions.Compiled);

        public const string ParamPattern = "^\\@[a-zA-Z](\\w)*$";
        private static Regex IsParamRegEx = new Regex(ParamPattern, RegexOptions.Compiled);

        public static HashSet<string> QueryOps =
            new HashSet<string>
            {
                "=", "<>", ">", ">=", "<", "<=",
                "matches",
                "like"
            };

        public static HashSet<string> ReservedWords =
            new HashSet<string> 
            { 
                "select",
                "from",
                "where",
                "limit",
                "value", 
                "id", 
                "count",
                "created",
                "lastmodified",
                "relevance"
            };

        private static JsonSerializer sm_jsonSerializer = new JsonSerializer();
    }
}
