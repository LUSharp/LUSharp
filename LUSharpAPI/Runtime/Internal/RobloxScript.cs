
using LUSharpAPI.Runtime.STL.Classes;

namespace LUSharpAPI.Runtime.Internal
{
    /// <summary>
    /// A base class that will be used to wrap Roblox scripts.
    /// </summary>
    public abstract class RobloxScript : Globals
    {
        /// <summary>
        /// The entry point for the script.
        /// </summary>
        public abstract void GameEntry();

    }
}