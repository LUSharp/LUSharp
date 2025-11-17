
using LUSharpTranspiler.Runtime.STL.LuaToC;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Runtime.STL.Types
{
    public interface IRBXScriptSignal
    {
    }
    public abstract class RBXScriptSignal : IRBXScriptSignal
    {
        /// <summary>
        /// Establishes a function to be called when the event fires. Returns an RBXScriptConnection object associated with the connection. 
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection Connect(Action function);

        /// <summary>
        /// Establishes a function to be called when the event fires. 
        /// Returns an RBXScriptConnection object associated with the connection. 
        /// When the event fires, the signal callback is executed in a desynchronized state.
        /// Using ConnectParallel is similar to, but more efficient than, using Connect followed by a call to task.desynchronize() in the signal handler.
        /// Note: Scripts that connect in parallel must be rooted under an Actor.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection ConnectParallel(Action function);

        /// <summary>
        /// Establishes a function to be called when the event fires. 
        /// Returns an RBXScriptConnection object associated with the connection.
        /// The behavior of Once is similar to Connect. However, instead of allowing multiple events to be received by the specified function, only the first event will be delivered. 
        /// Using Once also ensures that the connection to the function will be automatically disconnected prior the function being called.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection Once(Action function);

        /// <summary>
        /// Yields the current thread until the signal fires and returns the arguments provided by the signal.
        /// </summary>
        /// <returns></returns>
        public abstract object[] Wait();
    }
    public abstract class RBXScriptSignal<T1> : IRBXScriptSignal
    {
        /// <summary>
        /// Establishes a function to be called when the event fires. Returns an RBXScriptConnection object associated with the connection. 
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection Connect(Action<T1> function);

        /// <summary>
        /// Establishes a function to be called when the event fires. 
        /// Returns an RBXScriptConnection object associated with the connection. 
        /// When the event fires, the signal callback is executed in a desynchronized state.
        /// Using ConnectParallel is similar to, but more efficient than, using Connect followed by a call to task.desynchronize() in the signal handler.
        /// Note: Scripts that connect in parallel must be rooted under an Actor.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection ConnectParallel(Action<T1> function);

        /// <summary>
        /// Establishes a function to be called when the event fires. 
        /// Returns an RBXScriptConnection object associated with the connection.
        /// The behavior of Once is similar to Connect. However, instead of allowing multiple events to be received by the specified function, only the first event will be delivered. 
        /// Using Once also ensures that the connection to the function will be automatically disconnected prior the function being called.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection Once(Action<T1> function);

        /// <summary>
        /// Yields the current thread until the signal fires and returns the arguments provided by the signal.
        /// </summary>
        /// <returns></returns>
        public abstract object[] Wait();
    }
    public abstract class RBXScriptSignal<T1, T2> : IRBXScriptSignal
    {
        /// <summary>
        /// Establishes a function to be called when the event fires. Returns an RBXScriptConnection object associated with the connection. 
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection Connect(Action<T1, T2> function);

        /// <summary>
        /// Establishes a function to be called when the event fires. 
        /// Returns an RBXScriptConnection object associated with the connection. 
        /// When the event fires, the signal callback is executed in a desynchronized state.
        /// Using ConnectParallel is similar to, but more efficient than, using Connect followed by a call to task.desynchronize() in the signal handler.
        /// Note: Scripts that connect in parallel must be rooted under an Actor.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection ConnectParallel(Action<T1, T2> function);

        /// <summary>
        /// Establishes a function to be called when the event fires. 
        /// Returns an RBXScriptConnection object associated with the connection.
        /// The behavior of Once is similar to Connect. However, instead of allowing multiple events to be received by the specified function, only the first event will be delivered. 
        /// Using Once also ensures that the connection to the function will be automatically disconnected prior the function being called.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        public abstract RBXScriptConnection Once(Action<T1, T2> function);

        /// <summary>
        /// Yields the current thread until the signal fires and returns the arguments provided by the signal.
        /// </summary>
        /// <returns></returns>
        public abstract object[] Wait();
    }
}