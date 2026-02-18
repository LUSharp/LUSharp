using LUSharpAPI.Runtime.STL.Enums;
using LUSharpAPI.Runtime.STL.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpAPI.Runtime.STL.Classes.Instance.LuaSourceContainer
{
    public class BaseScript : LuaSourceContainer
    {
        public bool Disabled { get; set; }
        public bool Enabled { get; set; }
    }
}
