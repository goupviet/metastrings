using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace metastrings
{
    [TestClass]
    public class ErrorLogTests
    {
        [TestMethod]
        public void TestErrorLog()
        {
            using (var ctxt = TestUtils.GetCtxt())
            {
                for (int t = 1; t <= 3; ++t)
                {
                    ErrorLog.ClearAsync(ctxt).Wait();

                    ErrorLog.LogAsync(ctxt, 1, "10.0.0.1", "foo foo bar").Wait();
                    ErrorLog.LogAsync(ctxt, 2, "10.0.0.2", "blet monkey").Wait();

                    {
                        var logEntries = ErrorLog.QueryAsync(ctxt, "%foo%", 10).Result;
                        Assert.AreEqual(1, logEntries.Count);
                        Assert.AreEqual(1, logEntries[0].userId);
                        Assert.AreEqual("10.0.0.1", logEntries[0].ip);
                        Assert.AreEqual("foo foo bar", logEntries[0].msg);
                        Assert.IsTrue((DateTime.UtcNow - logEntries[0].when).TotalSeconds < 10);
                    }

                    {
                        var logEntries = ErrorLog.QueryAsync(ctxt, "blet%", 10).Result;
                        Assert.AreEqual(1, logEntries.Count);
                        Assert.AreEqual(2, logEntries[0].userId);
                        Assert.AreEqual("10.0.0.2", logEntries[0].ip);
                        Assert.AreEqual("blet monkey", logEntries[0].msg);
                        Assert.IsTrue((DateTime.UtcNow - logEntries[0].when).TotalSeconds < 10);
                    }
                }
            }
        }
    }
}
