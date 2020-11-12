using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace metastrings
{
    [TestClass]
    public class ItemNameValueTests
    {
        [TestMethod]
        public void TestItemNameValues()
        {
            using (var ctxt = TestUtils.GetCtxt())
            {
                int tableId = Tables.GetIdAsync(ctxt, "ape").Result;
                int bletNameId = Names.GetIdAsync(ctxt, tableId, "blet").Result;
                long monkeyValueId = Values.GetIdAsync(ctxt, "monkey").Result;

                long itemId0 = Items.GetIdAsync(ctxt, tableId, monkeyValueId).Result;
                long itemId1 = Items.GetIdAsync(ctxt, tableId, monkeyValueId).Result;
                Assert.AreEqual(itemId0, itemId1);

                Items.DeleteAsync(ctxt, itemId1).ConfigureAwait(false);
                long itemId2 = Items.GetIdAsync(ctxt, tableId, monkeyValueId).Result;
                Assert.AreNotEqual(itemId0, itemId2);

                var metaDict = new Dictionary<int, long>();
                int fredNameId = Names.GetIdAsync(ctxt, tableId, "fred").Result;
                long earnyValueId = Values.GetIdAsync(ctxt, "earny").Result;
                metaDict[fredNameId] = earnyValueId;
                Items.SetItemData(ctxt, itemId2, metaDict);
                ctxt.ProcessPostOpsAsync().Wait();

                var outMetaDict = Items.GetItemDataAsync(ctxt, itemId2).Result;
                Assert.AreEqual(1, outMetaDict.Count);
                Assert.IsTrue(outMetaDict.ContainsKey(fredNameId));
                Assert.AreEqual(earnyValueId, outMetaDict[fredNameId]);

                using (var msTrans = ctxt.BeginTrans())
                {
                    Define define = new Define() { table = "apelike" };
                    define.SetData("foo", "blet", "monkey");
                    define.SetData("foo", "something", "else");
                    ctxt.Cmd.DefineAsync(define).Wait();
                    msTrans.Commit();
                }

                {
                    GetRequest get = new GetRequest() { table = "apelike" };
                    get.values = new List<object> { "foo" };
                    var gotten = ctxt.Cmd.GetAsync(get).Result;

                    Assert.AreEqual(1, gotten.metadata.Count);

                    var result = gotten.metadata[0];
                    Assert.AreEqual("monkey", result["blet"]);
                    Assert.AreEqual("else", result["something"]);
                }
            }
        }
    }
}
