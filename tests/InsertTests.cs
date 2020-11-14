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
                {
                    var define = new Define() { table = "fun", key = "some" };
                    define.SetData("num", 42);
                    define.SetData("str", "foobar");
                    define.SetData("multi", "blet\nmonkey");
                    ctxt.Cmd.DefineAsync(define).Wait();
                }

                {
                    var define = new Define() { table = "fun", key = "another" };
                    define.SetData("num", 69);
                    define.SetData("str", "boofar");
                    define.SetData("multi", "ape\nagony");
                    ctxt.Cmd.DefineAsync(define).Wait();
                }

                {
                    var define = new Define() { table = "fun", key = "yetsome" };
                    define.SetData("num", 19);
                    define.SetData("str", "playful");
                    define.SetData("multi", "balloni\nbeats");
                    ctxt.Cmd.DefineAsync(define).Wait();
                }

                long itemId = Items.GetIdAsync(ctxt, Tables.GetIdAsync(ctxt, "fun").Result, Values.GetIdAsync(ctxt, "some").Result).Result;
                var itemData = NameValues.GetMetadataValuesAsync(ctxt, Items.GetItemDataAsync(ctxt, itemId).Result).Result;

                Assert.AreEqual(42.0, itemData["num"]);
                Assert.AreEqual("foobar", itemData["str"]);
                Assert.AreEqual("blet\nmonkey", itemData["multi"]);

                // NOTE: Full text search needs to see our recent changes
                ctxt.Db.ExecuteSql("OPTIMIZE TABLE bvalues"); 

                {
                    Select select =
                        Sql.Parse
                        (
                            "SELECT value, multi\n" +
                            $"FROM fun\n" +
                            "WHERE multi MATCHES @search"
                        );
                    select.AddParam("@search", "monkey");
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

                {
                    Define define = new Define() { table = "fun", key = "some" };
                    define.SetData("num", 43.0);
                    define.SetData("str", null); // remove the metadata

                    ctxt.Cmd.DefineAsync(define).Wait();
                }

                itemData = NameValues.GetMetadataValuesAsync(ctxt, Items.GetItemDataAsync(ctxt, itemId).Result).Result;
                Assert.IsTrue(!itemData.ContainsKey("str"));
                Assert.AreEqual(43.0, itemData["num"]);
                Assert.IsTrue(!itemData.ContainsKey("str"));

                var del = new Delete() { table = "fun" };
                del.AddValue("some");
                del.AddValue("another");
                del.AddValue("yetsome");
                ctxt.Cmd.DeleteAsync(del).Wait();

                {
                    Select select = new Select()
                    {
                        from = "fun",
                        select = new List<string> { "value" }
                    };
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                    {
                        if (reader.ReadAsync().Result) // should be gone
                            Assert.Fail();
                    }
                }

                {
                    Define numsFirst = new Define() { table = "numsFirst", key = 1 };
                    numsFirst.SetData("foo", 12);
                    numsFirst.SetData("blet", "79");
                    ctxt.Cmd.DefineAsync(numsFirst).Wait();

                    Define numsNext = new Define() { table = "numsFirst", key = 2 };
                    numsNext.SetData("foo", 15);
                    numsNext.SetData("blet", "63");
                    ctxt.Cmd.DefineAsync(numsNext).Wait();

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
