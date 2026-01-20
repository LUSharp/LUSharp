
using LUSharpAPI.Runtime.STL.Classes;
using LUSharpAPI.Runtime.STL.Classes.Instance.LuaSourceContainer;
using LUSharpAPI.Runtime.STL.Services;
using LUSharpAPI.Runtime.STL.Types;
using System;

namespace LUSharpAPI
{
    public abstract class Globals
    {
        #region LUA Global Functions
        public static (bool result, string errorMessage) assert(bool condition, string errorMessage)
        {
            throw new NotImplementedException();
        }

        public static void error(string message, double level)
        {
            throw new NotImplementedException();
        }

        public static double gcinfo()
        {
            throw new NotImplementedException();
        }

        public static double getmetatable(string what)
        {
            throw new NotImplementedException();
        }

        public static void print(params string[] message)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Roblox Globals

        public Enums Enum { get; set; }
        public DataModel game { get; set; }
            
        //public Plugin plugin { get; set; }
        public List<object> shared { get; set; }
        
        public LuaSourceContainer script { get; set; }
        public Workspace workspace { get; set; }

        public extern double elapsedTime();

        public extern double tick();

        public extern double time();

        public extern string version();

        public extern void warn(params string[] message); 

        #endregion
    }
}