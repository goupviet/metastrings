using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace metastrings
{
    class Program
    {
        static async Task Main()
        {
            // Let's keep it simple and use SQLite
            // We just need to define a path to the database
            // If the file does not exist, an empty database is automatically created
            string dbFilePath = 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "cars.db");

            // Create the Context object and pass in our database file path
            using (var ctxt = new Context(dbFilePath))
            {
                // Add database records using a helper function...so many cars...
                Console.WriteLine("Adding cars...");
                await AddCarAsync(ctxt, 1982, "Chrysler", "LeBaron");
                await AddCarAsync(ctxt, 1983, "Toyota", "Tercel");
                await AddCarAsync(ctxt, 1998, "Toyota", "Tacoma");
                await AddCarAsync(ctxt, 2001, "Nissan", "Xterra");
                await AddCarAsync(ctxt, 1987, "Nissan", "Pathfinder");
                //...

                // Select data out of the database using a basic dialect of SQL
                // No JOINs
                // All WHERE criteria must use parameters
                // All ORDER BY colums must be in SELECT column list
                // Here we gather the "value" pseudo-column, the row ID created by the helper function
                Console.WriteLine("Getting old cars...");
                var oldCarGuids = new List<object>();
                var select = 
                    Sql.Parse("SELECT value, year, make, model FROM cars WHERE year < @year ORDER BY year ASC");
                select.AddParam("@year", 1990);
                using (var reader = await ctxt.ExecSelectAsync(select))
                {
                    while (reader.Read())
                    {
                        oldCarGuids.Add(reader.GetValue(0));
                        Console.WriteLine(reader.GetDouble(1) + ": " + reader.GetString(2) + " - " + reader.GetString(3));
                    }
                }

                // Use the list of row IDs to delete some rows
                Console.WriteLine("Deleting old cars...");
                await ctxt.Cmd.DeleteAsync("cars", oldCarGuids);

                // Drop the table to keep things clean for the new run
                Console.WriteLine("Cleaning up...");
                await ctxt.Cmd.DropAsync("cars");

                Console.WriteLine("All done.");
            }
        }

        // Given info about a car, add it to the database using the Context object
        static async Task AddCarAsync(Context ctxt, int year, string make, string model)
        {
            // The Define class is used to UPSERT
            // No need to create tables, just refer to them and the database takes care of it
            // Same goes for columns, just add data into the columns and it just works
            // The second parameter to the Define constructor is the row ID
            // This would be a natural primary key, but we just use a GUID to keep things simple
            var define = new Define("cars", Guid.NewGuid().ToString());
            define.Set("year", year);
            define.Set("make", make);
            define.Set("model", model);
            await ctxt.Cmd.DefineAsync(define);
        }
    }
}
