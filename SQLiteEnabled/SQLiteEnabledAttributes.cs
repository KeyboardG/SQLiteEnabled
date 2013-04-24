using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteEnabled
{
    [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
    public class SQLiteEnabledSerialization : System.Attribute
    {
        public bool Serialize;

        public SQLiteEnabledSerialization(bool serialize = true)
        {
            Serialize = serialize;
        }
    }



}
