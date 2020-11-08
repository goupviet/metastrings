using System;
using System.Collections.Generic;
using System.IO;

namespace metastrings
{
    public static class TestUtils
    {
        public static Context GetCtxt()
        {
            using (var ctxt = new Context("metastrings"))
                NameValues.Reset(ctxt);

            return new Context("metastrings");
        }
    }
}
