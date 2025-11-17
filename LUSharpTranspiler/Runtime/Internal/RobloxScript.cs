
using LUSharpTranspiler.Runtime.STL.Classes;

namespace LUSharpTranspiler.Runtime.Internal
{
    /// <summary>
    /// A base class that will be used to wrap Roblox scripts.
    /// </summary>
    public abstract class RobloxScript
    {
        public DataModel game { get; set; }
        /// <summary>
        /// The entry point for the script.
        /// </summary>
        public abstract void GameEntry();

    }
}