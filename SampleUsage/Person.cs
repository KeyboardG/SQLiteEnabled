using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteEnabled.SampleUsage
{
    class Person :SQLiteEnabled
    {
        // Define additional fields particular to this type of object.
        private string firstName;
        public string FirstName
        {
            get { return firstName; }
            set { firstName = value; }
        }

        private string lastName;
        public string LastName
        {
            get { return lastName; }
            set { lastName = value; }
        }

        private DateTime dateOfBirth;
        public DateTime DateOfBirth
        {
            get { return dateOfBirth; }
            set { dateOfBirth = value; }
        }


        private Int64 age;  // Note that Int64 is required to map to a 64-bit SQLite Int.
        public Int64 Age
        {
            get { return age; }
            set { age = value; }
        }

        private bool isAlive;
        public bool IsAlive
        {
            get { return isAlive; }
            set { isAlive = value; }
        }


        // SQLite Functions ------------------------------

        /// <summary>
        /// Function simply converts a dynamic of a Person object into a strongly typed version.
        /// NOTE: Look into building this into the SQLiteEnabled base class using the Activator.CreateInstance().
        /// </summary>
        /// <param name="dynamicPerson">A person object retrieved from the SQLite database</param>
        /// <returns>Strongly typed version of the Person object</returns>
        public static Person FromDynamicConverter(dynamic dynamicPerson)
        {
            Person Returned = new Person();
            Returned.ID = dynamicPerson.ID;
            Returned.RetreivedValue = dynamicPerson.RetreivedValue;
            Returned.FirstName = dynamicPerson.FirstName;
            Returned.LastName = dynamicPerson.LastName;
            Returned.DateOfBirth = dynamicPerson.DateOfBirth;
            Returned.Age = dynamicPerson.Age;
            Returned.IsAlive = dynamicPerson.IsAlive;
            
            return Returned;
        }
    }
}
