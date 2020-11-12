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
                    ErrorLog.Clear(ctxt);

                    ErrorLog.Log(ctxt, "foo foo bar");
                    ErrorLog.Log(ctxt, "blet monkey");

                    {
                        var logEntries = ErrorLog.QueryAsync(ctxt, "%foo%", 10).Result;
                        Assert.AreEqual(1, logEntries.Count);
                        Assert.AreEqual("foo foo bar", logEntries[0].msg);
                        Assert.IsTrue((DateTime.UtcNow - logEntries[0].when).TotalSeconds < 10);
                    }

                    {
                        var logEntries = ErrorLog.QueryAsync(ctxt, "blet%", 10).Result;
                        Assert.AreEqual(1, logEntries.Count);
                        Assert.AreEqual("blet monkey", logEntries[0].msg);
                        Assert.IsTrue((DateTime.UtcNow - logEntries[0].when).TotalSeconds < 10);
                    }
                }
            }
        }
    }
}
