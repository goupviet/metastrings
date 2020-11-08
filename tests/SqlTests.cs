﻿using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace metastrings
{
    [TestClass]
    public class SqlTests
    {
        [TestMethod]
        public void TestSql()
        {
            using (var ctxt = TestUtils.GetCtxt())
            {
                // No tables, nothing should still work.
                {
                    var select = Sql.Parse("SELECT somethin\nFROM nothin");
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                        Assert.IsFalse(reader.Read());
                }

                // Add a row.
                {
                    var define = new Define() { table = "somethin" };
                    define.SetData("foo", "blet", "monkey");
                    using (var cmd = new Command(ctxt))
                        cmd.DefineAsync(define).Wait();
                }

                // Add another row.
                {
                    var define = new Define() { table = "somethin" };
                    define.SetData("bar", "flub", "snake");
                    using (var cmd = new Command(ctxt))
                        cmd.DefineAsync(define).Wait();
                }

                // Have a table now, but bogus SELECT column
                {
                    var select = Sql.Parse("SELECT nothin\nFROM somethin");
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.IsTrue(reader.IsDBNull(0));

                        Assert.IsTrue(reader.Read());
                        Assert.IsTrue(reader.IsDBNull(0));
                                
                        Assert.IsFalse(reader.Read());
                    }
                }

                // Have a table now, but bogus WHERE column bdfadf
                {
                    var select = Sql.Parse("SELECT nothin\nFROM somethin\nWHERE value = @foo AND bdfadf = @bdfadf");
                    select.AddParam("@foo", "foo");
                    select.AddParam("@bdfadf", 12.0);
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                        Assert.IsFalse(reader.Read());
                }

                // See that it all works
                {
                    var select = Sql.Parse("SELECT blet\nFROM somethin\nWHERE value = @foo");
                    select.AddParam("@foo", "foo");
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual("monkey", reader.GetString(0));
                        Assert.IsFalse(reader.Read());
                    }
                }

                {
                    var select = Sql.Parse("SELECT flub\nFROM somethin\nWHERE value = @bar");
                    select.AddParam("@bar", "bar");
                    using (var reader = ctxt.ExecSelectAsync(select).Result)
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual("snake", reader.GetString(0));
                        Assert.IsFalse(reader.Read());
                    }
                }
            }
        }
    }
}
