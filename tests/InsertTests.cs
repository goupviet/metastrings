using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace metastrings
{
    [TestClass]
    public class DefineTest
    {
        [TestMethod]
        public void TestDefine()
        {
            using (var ctxt = TestUtils.GetCtxt())
            {
                var define = new Define() { table = "fun" };
                define.SetData("some", "num", 42);
                define.SetData("some", "str", "foobar");
                define.SetData("some", "multi", "blet\nmonkey");

                using (Command cmd = new Command(ctxt))
                    cmd.DefineAsync(define).Wait();

                long itemId = Items.GetIdAsync(ctxt, Tables.GetIdAsync(ctxt, define.table).Result, Values.GetIdAsync(ctxt, "some").Result).Result;
                var itemData = NameValues.GetMetadataValuesAsync(ctxt, Items.GetItemDataAsync(ctxt, itemId).Result).Result;

                Assert.AreEqual(42.0, itemData["num"]);
                Assert.AreEqual("foobar", itemData["str"]);
                Assert.AreEqual("blet\nmonkey", itemData["multi"]);

                {
                    Select select =
                        Sql.Parse
                        (
                            "SELECT value, multi\n" +
                            $"FROM {define.table}\n" +
                            "WHERE multi MATCHES @search"
                        );
                    select.AddParam("@search", "blet");
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                    {
                        if (!reader.ReadAsync().Result)
                            Assert.Fail();

                        var val = reader.GetString(0);
                        var str = reader.GetString(1);

                        Assert.AreEqual("some", val);
                        Assert.AreEqual("blet\nmonkey", str);

                        if (reader.ReadAsync().Result)
                            Assert.Fail();
                    }
                }

                define.SetData("some", "num", 43.0);
                define.SetData("some", "str", null); // remove the metadata

                using (Command cmd = new Command(ctxt))
                    cmd.DefineAsync(define).Wait();

                itemData = NameValues.GetMetadataValuesAsync(ctxt, Items.GetItemDataAsync(ctxt, itemId).Result).Result;
                Assert.IsTrue(!itemData.ContainsKey("str"));
                Assert.AreEqual(43.0, itemData["num"]);
                Assert.IsTrue(!itemData.ContainsKey("str"));

                using (Command cmd = new Command(ctxt))
                    cmd.DeleteAsync(new Delete() { table = define.table, values = new List<object> { "some" } }).Wait();

                {
                    Select select = new Select()
                    {
                        from = define.table,
                        select = new List<string> { "value" }
                    };
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                    {
                        if (reader.ReadAsync().Result) // should be gone
                            Assert.Fail();
                    }
                }

                {
                    Define numsFirst = new Define() { table = "numsFirst" };
                    numsFirst.SetData(1, "foo", 12);
                    numsFirst.SetData(1, "blet", "79");
                    using (Command cmd = new Command(ctxt))
                        cmd.DefineAsync(numsFirst).Wait();

                    Define numsNext = new Define() { table = "numsFirst" };
                    numsNext.SetData(2, "foo", 15);
                    numsNext.SetData(2, "blet", "63");
                    using (Command cmd = new Command(ctxt))
                        cmd.DefineAsync(numsNext).Wait();

                    Select select = Sql.Parse("SELECT value, foo, blet\nFROM numsFirst");
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                    {
                        while (reader.Read())
                        {
                            if (reader.GetInt64(0) == 1)
                            {
                                Assert.AreEqual(12, reader.GetDouble(1));
                                object strObj = reader.GetValue(2); // FORNOW
                                Assert.AreEqual("79", reader.GetString(2));
                            }
                            else if (reader.GetInt64(0) == 2)
                            {
                                Assert.AreEqual(15, reader.GetDouble(1));
                                Assert.AreEqual("63", reader.GetString(2));
                            }
                            else
                            {
                                Assert.Fail();
                            }
                        }
                    }
                }
            }
        }
    }
}
