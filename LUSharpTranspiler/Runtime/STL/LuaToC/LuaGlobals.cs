
namespace LUSharpTranspiler.Runtime.STL.LuaToC
{
    public class LuaGlobals
    {
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

    }
}