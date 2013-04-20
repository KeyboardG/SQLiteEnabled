using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SQLite;
using SQLiteEnabled;


namespace SQLiteEnabled.SampleUsage
{
    class Program
    {
        static void Main(string[] args)
        {
            // Connect or create the SQLite database.
            var SQLiteConnection = new SQLiteConnection("Data Source=Sample.db;New=True;Version=3;Max Pool Size=100;");
            SQLiteConnection.Open();

            // Create the table if it doesn't exist.
            if (!SQLiteEnabled.DoesSQLiteTableExist(SQLiteConnection, typeof(Person)))
            {
                Console.WriteLine(SQLiteEnabled.CreateSQLiteTable(SQLiteConnection, typeof(Person)));
            }

            // Retreive any existing people.
            // See .ConvertAll<> is what converts from the dynamic to the strongly typed object.
            List<Person> People = SQLiteEnabled.RetreiveFromDataBase(SQLiteConnection, typeof(Person)).ConvertAll<Person>(p => Person.FromDynamicConverter(p));
                
            // Creat a bunch of random new people.
            for (int i = 0; i < 1000; i++)
            {
                var NewGuy = new Person();
                NewGuy.FirstName = "John";
                NewGuy.LastName = "Doe";
                NewGuy.Age = i;
                People.Add(NewGuy);
            }

            // Commit the changes. 
            // Still a little ugly glue to convert from the strongly typed list to dynamic.
            // What I don't like about dynamic in .Net 4.5 is that you can pass a strongly typed object to a function's dynamic parameter,
            // but you cannot pass a strongly typed list into a functions list of dynamic parameter.
            SQLiteEnabled.CommitData(SQLiteConnection, People.ConvertAll<dynamic>(p => p), typeof(Person));

            Console.WriteLine("Derp");
            Console.ReadKey();
        }
    }
}
