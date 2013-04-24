namespace SQLiteEnabled
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SQLiteEnabled
    {
        // Serves as the auto-incrementing 
        private Int64 id;
        public Int64 ID
        {
            get { return id; }
            set
            {
                if (id == 0)
                {
                    id = value;
                }
                else
                {
                    throw new Exception("Cannot change ID on existing object.");
                }
            }
        }


        // Contains the values of the fields as retreived from the database.
        private byte[] retreivedValue;

        [SQLiteEnabledSerialization(false)]
        public byte[] RetreivedValue
        {
            get { return retreivedValue; }
            set { retreivedValue = value; }
        }


        // Override in subclasses to map additional fields.
        public static SQLiteEnabled DynamicConverter(dynamic dynamicSQLiteEnabled)
        {
            SQLiteEnabled Returned = new SQLiteEnabled();
            Returned.ID = dynamicSQLiteEnabled.ID;
            return Returned;
        }


        /// <summary>
        /// Function determines if the table to store the passed object exists in the SQLite database pointed to by the passed connection.
        /// </summary>
        /// <param name="sqliteConnection">The SQLiteConnection to </param>
        /// <param name="sqliteEnabledType">Type of the object being </param>
        /// <returns>True if the SQLite table exists, false otherwise.</returns>
        public static bool DoesSQLiteTableExist(SQLiteConnection sqliteConnection, Type sqliteEnabledType)
        {
            // Will only work with classes that are sqliteEnabledType.
            if (sqliteEnabledType.BaseType != typeof(SQLiteEnabled)) { return false; }

            var CheckCommand = sqliteConnection.CreateCommand();
            CheckCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name= @param1;";
            CheckCommand.Parameters.Add(new SQLiteParameter("@param1", sqliteEnabledType.Name));

            var Catch = CheckCommand.ExecuteReader(CommandBehavior.SingleResult);

            return (Catch.GetValue(0).ToString().Trim() != String.Empty);
        }


        /// <summary>
        /// Creates SQLite table in the passed connection for the passed object implementing iSQLitable.
        /// </summary>
        /// <param name="sqliteConnection">The SQLiteConnection object which points to the SQLite database.</param>
        /// <param name="sqliteEnabledType">Type of the object being </param>
        /// <returns>True on success. False on failure.</returns>
        public static bool CreateSQLiteTable(SQLiteConnection sqliteConnection, Type sqliteEnabledType)
        {
            // Will only work with classes that are sqliteEnabledType.
            if (sqliteEnabledType.BaseType != typeof(SQLiteEnabled)) { return false; }

            string CreateCommandText = "Create Table " + sqliteEnabledType.Name + "(";

            foreach (var ThisMember in sqliteEnabledType.GetMembers().Where(m => m.MemberType == System.Reflection.MemberTypes.Property).ToList())
            {
                if (!IsPropertySQLiteEnabledSerialized(ThisMember)) { continue; }
                
                string ThisPropertyType = ThisMember.ToString().Split(' ')[0].ToUpper();

                // This type mapping relies on the fact that SQLite actually has only a couple actual data types that are mapped.
                CreateCommandText += ThisMember.Name + " ";

                if (ThisPropertyType.Contains("INT"))
                {
                    CreateCommandText += "INTEGER";

                    // This needs to read the method on the type GetPrimaryKeyFieldName().
                    if (ThisMember.Name == "ID")
                    {
                        CreateCommandText += " PRIMARY KEY AUTOINCREMENT ";
                    }
                }
                else if (ThisPropertyType.Contains("DEC") || ThisPropertyType.Contains("DOUBLE"))
                {
                    CreateCommandText += "REAL";
                }
                else if (ThisPropertyType.Contains("STRING") || ThisPropertyType.Contains("CHAR"))
                {
                    CreateCommandText += "TEXT";
                }
                else if (ThisPropertyType.Contains("DATE"))
                {
                    CreateCommandText += "DATETIME";
                }

                CreateCommandText += ", ";
            }
            CreateCommandText = CreateCommandText.SubstringBeforeLast(',') + ")";


            var CreateCommand = sqliteConnection.CreateCommand();
            CreateCommand.CommandText = CreateCommandText;

            return 0 <= CreateCommand.ExecuteNonQuery();
        }


        /// <summary>
        /// Retrieves data from the SQLite database for the passed type and returns a list of that object.
        /// The caller can simply .ConvertAll() to its base type by implementing a function with a simple property mapping.
        /// </summary>
        /// <param name="sqliteConnection">The SQLiteConnection object which points to the SQLite database.</param>
        /// <param name="sqliteEnabledType">Type of the object being </param>
        /// <returns>A list of sqliteEnabledType as dynamics.</returns>
        public static List<dynamic> RetreiveFromDataBase(SQLiteConnection sqliteConnection, Type sqliteEnabledType)
        {
            var ResultList = new List<dynamic>();

            // Will only work with classes that are sqliteEnabledType.
            if (sqliteEnabledType.BaseType != typeof(SQLiteEnabled)) { return ResultList; }
        
            var SelectCommand = sqliteConnection.CreateCommand();
            SelectCommand.CommandText = "SELECT * FROM " + sqliteEnabledType.Name;

            var SQLiteReader = SelectCommand.ExecuteReader();
            while (SQLiteReader.Read())
            {
                dynamic ObjectToAdd = Activator.CreateInstance(sqliteEnabledType);
                foreach (var ThisMember in sqliteEnabledType.GetMembers().Where(m => m.MemberType == System.Reflection.MemberTypes.Property).ToList())
                {
                    if (!IsPropertySQLiteEnabledSerialized(ThisMember)) { continue; }
                    var PropertyNameToMap = ThisMember.Name;
                    var PropertyToMap = sqliteEnabledType.GetProperty(PropertyNameToMap);

                    PropertyToMap.SetValue(ObjectToAdd, SQLiteReader[PropertyNameToMap]);   // Set the looked up property on the dynamic object.
                }

                ObjectToAdd.RetreivedValue = ObjectToAdd.ComputeByteArrayValue();
                ResultList.Add(ObjectToAdd);
            }

            return ResultList;
        }


        /// <summary>
        /// Commits all objects in the passed list of SQLiteEnabled
        /// </summary>
        /// <param name="sqliteConnection">The SQLiteConnection object which points to the SQLite database.</param>
        /// <param name="dataSet">List of SQLiteEnabled objects to commit to the database.</param>
        /// <param name="sqliteEnabledType">Type of objects contained withing dataSet.</param>
        /// <returns>True upon completion.</returns>
        public static bool CommitData(SQLiteConnection sqliteConnection, List<dynamic> dataSet, Type sqliteEnabledType)
        {
            // Will only work with classes that are sqliteEnabledType.
            if (sqliteEnabledType.BaseType != typeof(SQLiteEnabled)) { return false; }

            // Wrap the entire process in a transaction for performance and to make this function atomic.
            var TransactionCommand = sqliteConnection.CreateCommand();
            TransactionCommand.CommandText = "BEGIN TRANSACTION";
            TransactionCommand.ExecuteNonQuery();

            foreach (var ThisObject in dataSet)
            {
                // Insert new records and update 
                if (sqliteEnabledType.GetProperty("ID").GetValue(ThisObject) == 0)
                {
                    InsertObject(sqliteConnection, ThisObject, sqliteEnabledType);
                }
                else
                {
                    // Only update is the object has changed.
                    if (!Enumerable.SequenceEqual<byte>(ThisObject.RetreivedValue, ThisObject.ComputeByteArrayValue()))
                    {
                        UpdateObject(sqliteConnection, ThisObject, sqliteEnabledType);
                    }
                }
            }

            TransactionCommand.CommandText = "END TRANSACTION";
            TransactionCommand.ExecuteNonQuery();
            return true;
        }


        /// <summary>
        /// Inserts sqliteEnabledObject into the database indicated by sqliteConnection.
        /// </summary>
        /// <param name="sqliteConnection">The SQLiteConnection object which points to the SQLite database.</param>
        /// <param name="sqliteEnabledObject">The SQLiteEnabled object to insert.</param>
        /// <param name="sqliteEnabledType">The type of SQLiteEnabled object.</param>
        /// <returns>True on success or throws on exception.</returns>
        public static bool InsertObject(SQLiteConnection sqliteConnection, dynamic sqliteEnabledObject, Type sqliteEnabledType)
        {
            var InsertCommand = sqliteConnection.CreateCommand();
            InsertCommand.CommandText = "INSERT INTO " + sqliteEnabledType.Name + "(";

            foreach (var ThisMember in sqliteEnabledType.GetMembers().Where(m => m.MemberType == System.Reflection.MemberTypes.Property).ToList())
            {
                if (ThisMember.Name == "ID") { continue; }
                if (!IsPropertySQLiteEnabledSerialized(ThisMember)) { continue; }

                string ThisPropertyName = ThisMember.Name;

                InsertCommand.CommandText += ThisPropertyName + ", ";
            }
            InsertCommand.CommandText = InsertCommand.CommandText.SubstringBeforeLast(',') + ") VALUES (";

            foreach (var ThisMember in sqliteEnabledType.GetMembers().Where(m => m.MemberType == System.Reflection.MemberTypes.Property).ToList())
            {
                if (ThisMember.Name == "ID") { continue; }
                if (!IsPropertySQLiteEnabledSerialized(ThisMember)) { continue; }

                string ThisPropertyType = ThisMember.ToString().Split(' ')[0].ToUpper();
                string ThisPropertyName = ThisMember.Name;

                if (ThisPropertyType.Contains("INT") || ThisPropertyType.Contains("DEC") || ThisPropertyType.Contains("DOUBLE"))
                {
                    // Numeric types can put into the command.
                    InsertCommand.CommandText += sqliteEnabledType.GetProperty(ThisPropertyName).GetValue(sqliteEnabledObject).ToString();
                }
                else if (ThisPropertyType.Contains("STRING") || ThisPropertyType.Contains("CHAR") || (ThisPropertyType.Contains("DATE")))
                {
                    // Character and DateTime types get wrapped in quotes.
                    InsertCommand.CommandText += "'" + sqliteEnabledType.GetProperty(ThisPropertyName).GetValue(sqliteEnabledObject).ToString() + "'";
                }

                InsertCommand.CommandText += ", ";
            }

            InsertCommand.CommandText = InsertCommand.CommandText.SubstringBeforeLast(',') + ")";

            // Execute update or insert.
            if (0 > InsertCommand.ExecuteNonQuery())
            {
                throw new Exception("Error commiting data");
            }

            // Read back inserted id.
            var SelectCommand = sqliteConnection.CreateCommand();
            SelectCommand.CommandText = "SELECT ID FROM " + sqliteEnabledType.Name + " ORDER BY ID DESC LIMIT 1";
            var SQLiteReader = SelectCommand.ExecuteReader();
            if( SQLiteReader.Read())
            {
                sqliteEnabledObject.ID = (Int64)SQLiteReader["ID"];
            }else{
                throw new Exception("Error getting last inserted id.");
            }

            return true;
        }


        /// <summary>
        /// Updates sqliteEnabledObject in the database indicated by sqliteConnection.
        /// </summary>
        /// <param name="sqliteConnection">The SQLiteConnection object which points to the SQLite database.</param>
        /// <param name="sqliteEnabledObject">The SQLiteEnabled object to update.</param>
        /// <param name="sqliteEnabledType">The type of SQLiteEnabled object.</param>
        /// <returns>True on success or throws on exception.</returns>
        public static bool UpdateObject(SQLiteConnection sqliteConnection, dynamic sqliteEnabledObject, Type sqliteEnabledType)
        {
            var UpdateCommand = sqliteConnection.CreateCommand();
            UpdateCommand.CommandText = "UPDATE " + sqliteEnabledType.Name + " SET ";

            // Insert all the field settings except for ID.
            foreach (var ThisMember in sqliteEnabledType.GetMembers().Where(m => m.MemberType == System.Reflection.MemberTypes.Property).ToList())
            {
                if (ThisMember.Name == "ID") { continue; }
                if (!IsPropertySQLiteEnabledSerialized(ThisMember)) { continue; }
                string ThisPropertyType = ThisMember.ToString().Split(' ')[0].ToUpper();
                string ThisPropertyName = ThisMember.Name;

                UpdateCommand.CommandText += ThisPropertyName + "=";
   
                if (ThisPropertyType.Contains("INT") || ThisPropertyType.Contains("DEC") || ThisPropertyType.Contains("DOUBLE"))
                {
                    // Numeric types can put into the command.
                    UpdateCommand.CommandText += sqliteEnabledType.GetProperty(ThisPropertyName).GetValue(sqliteEnabledObject).ToString();
                }
                else if (ThisPropertyType.Contains("STRING") || ThisPropertyType.Contains("CHAR") || (ThisPropertyType.Contains("DATE")))
                {
                    // Character and DateTime types get wrapped in quotes.
                    UpdateCommand.CommandText += "'" + sqliteEnabledType.GetProperty(ThisPropertyName).GetValue(sqliteEnabledObject).ToString() + "'";
                }

                UpdateCommand.CommandText += ", ";
            }

            UpdateCommand.CommandText = UpdateCommand.CommandText.SubstringBeforeLast(',');
            UpdateCommand.CommandText += " WHERE ID=" + sqliteEnabledObject.ID.ToString();

            // Execute update or insert.
            if (0 > UpdateCommand.ExecuteNonQuery())
            {
                throw new Exception("Error commiting data");
            }
            
            return true;
        }


        /// <summary>
        /// Function computes all the fields contained in the SQLiteEnabled object to a single byte array which can be stored and compared
        /// later to check if the object has been modified.
        /// </summary>
        /// <returns>A byte array containing the values from all the fields on the object.</returns>
        private byte[] ComputeByteArrayValue()
        {
            var ComputedValue = new List<byte>();

            foreach (var ThisMember in this.GetType().GetMembers().Where(m => m.MemberType == System.Reflection.MemberTypes.Property).ToList())
            {
                if (!IsPropertySQLiteEnabledSerialized(ThisMember)) { continue; }

                string ThisPropertyType = ThisMember.ToString().Split(' ')[0].ToUpper();
                string ThisPropertyName = ThisMember.Name;

                if (ThisPropertyType.Contains("INT") || ThisPropertyType.Contains("DEC") || ThisPropertyType.Contains("DOUBLE"))
                {
                    if (ThisPropertyType.Contains("INT"))
                    {
                        // 64-bit intetegers map to long.
                        long Value = (long)(this.GetType().GetProperty(ThisPropertyName).GetValue(this));
                        ComputedValue.AddRange(BitConverter.GetBytes(Value).ToList());
                    }else{
                        Double Value = (double)(this.GetType().GetProperty(ThisPropertyName).GetValue(this));
                        ComputedValue.AddRange(BitConverter.GetBytes(Value).ToList());
                    }
                }
                else if (ThisPropertyType.Contains("STRING") || ThisPropertyType.Contains("CHAR"))
                {
                    foreach (char c in this.GetType().GetProperty(ThisPropertyName).GetValue(this).ToString().ToCharArray())
                    {
                        ComputedValue.AddRange(BitConverter.GetBytes(c).ToList());
                    }
                }
                else if (ThisPropertyType.Contains("DATE"))
                {
                    DateTime Value = (DateTime)(this.GetType().GetProperty(ThisPropertyName).GetValue(this));

                    ComputedValue.AddRange(BitConverter.GetBytes(Value.Year).ToList());
                    ComputedValue.AddRange(BitConverter.GetBytes(Value.Month).ToList());
                    ComputedValue.AddRange(BitConverter.GetBytes(Value.Day).ToList());
                }
            }

            return ComputedValue.ToArray();
        }


        /// <summary>
        /// Function determines if a property in a SQLiteEnabled object is set to be serialized into the database.
        /// </summary>
        /// <param name="ThisMember">A property on a SQLiteEnabled object.</param>
        /// <returns>False if the property has the attribute SQLiteEnabledSerialization with Serialize set to false.</returns>
        private static bool IsPropertySQLiteEnabledSerialized(System.Reflection.MemberInfo ThisMember)
        {
            System.Attribute SerializeAttribute = System.Attribute.GetCustomAttribute(ThisMember, typeof(SQLiteEnabledSerialization));
            if (SerializeAttribute != null && ((SQLiteEnabledSerialization)SerializeAttribute).Serialize == false)
            {
                return false;
            }

            return true;
        }
    }
}
