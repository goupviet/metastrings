using System;
using System.Threading.Tasks;
using System.IO;

using metastrings;

namespace MsFileIndexer
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("MsFileIndex -load <dir path> OR -query \"<query terms>\"");
                return 0;
            }

            switch (args[0])
            {
                case "-load":
                    {
                        using (var ctxt = new Context())
                        {
                            string dirPath = args[1];
                            string[] filePaths = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                            Console.WriteLine("Files found: " + filePaths.Length);

                            int outYet = 0;
                            Define define = new Define() { table = "files" }; // reuse the define
                            Command cmd = ctxt.Cmd; // reuse the cmd
                            foreach (string filePath in filePaths)
                            {
                                define.metadata.Clear();

                                define.key = filePath;
                                define.Set("tokens", filePath.Replace(Path.DirectorySeparatorChar, ' '));

                                await cmd.DefineAsync(define);

                                if ((++outYet % 1000) == 0)
                                    Console.WriteLine(outYet);
                            }
                        }
                    }
                    break;

                case "-query":
                    {
                        string query = args[1];
                        using (var ctxt = new Context())
                        {
                            var select = Sql.Parse("SELECT value FROM files WHERE tokens MATCHES @query");
                            select.AddParam("@query", query);
                            var resultFilePaths = await ctxt.ExecListAsync<string>(select);
                            Console.WriteLine("Results: " + resultFilePaths.Count);
                            foreach (var result in resultFilePaths)
                                Console.WriteLine(result);
                        }
                    }
                    break;
            }

            return 0;
        }
    }
}
