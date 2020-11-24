using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace metastrings
{
    [TestClass]
    public class ListDictionaryTests
    {
        [TestMethod]
        public void TestListDictioary()
        {
            ListDictionary<string, int> dict = new ListDictionary<string, int>();

            dict.Add("foo", 1);
            dict.Add("bar", 2);

            Assert.AreEqual(1, dict["foo"]);
            Assert.AreEqual(2, dict["bar"]);

            Assert.AreEqual(2, dict.Count);

            Assert.AreEqual("foo", dict.FirstKey);

            Assert.AreEqual("foo", dict.Entries.First().Key);
            Assert.AreEqual(1, dict.Entries.First().Value);

            Assert.AreEqual("bar", dict.Entries.Last().Key);
            Assert.AreEqual(2, dict.Entries.Last().Value);

            Assert.IsTrue(dict.Keys.Contains("foo"));
            Assert.IsTrue(dict.Values.Contains(1));

            Assert.IsTrue(dict.Keys.Contains("bar"));
            Assert.IsTrue(dict.Values.Contains(2));

            Assert.IsTrue(dict.ContainsKey("foo"));
            Assert.IsTrue(dict.ContainsKey("bar"));
            Assert.IsTrue(!dict.ContainsKey("blet"));
        }
    }
}
