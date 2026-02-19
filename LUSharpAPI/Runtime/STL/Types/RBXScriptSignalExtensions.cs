
using LUSharpAPI.Runtime.STL.LuaToC;

namespace LUSharpAPI.Runtime.STL.Types
{
    public abstract class RBXScriptSignal<T1, T2, T3> : IRBXScriptSignal
    {
        public abstract RBXScriptConnection Connect(Action<T1, T2, T3> function);
        public abstract RBXScriptConnection ConnectParallel(Action<T1, T2, T3> function);
        public abstract RBXScriptConnection Once(Action<T1, T2, T3> function);
        public abstract object[] Wait();

        public static RBXScriptConnection operator +(RBXScriptSignal<T1, T2, T3> signal, Action<T1, T2, T3> action)
        {
            return signal.Connect(action);
        }
    }

    public abstract class RBXScriptSignal<T1, T2, T3, T4> : IRBXScriptSignal
    {
        public abstract RBXScriptConnection Connect(Action<T1, T2, T3, T4> function);
        public abstract RBXScriptConnection ConnectParallel(Action<T1, T2, T3, T4> function);
        public abstract RBXScriptConnection Once(Action<T1, T2, T3, T4> function);
        public abstract object[] Wait();

        public static RBXScriptConnection operator +(RBXScriptSignal<T1, T2, T3, T4> signal, Action<T1, T2, T3, T4> action)
        {
            return signal.Connect(action);
        }
    }

    public abstract class RBXScriptSignal<T1, T2, T3, T4, T5> : IRBXScriptSignal
    {
        public abstract RBXScriptConnection Connect(Action<T1, T2, T3, T4, T5> function);
        public abstract RBXScriptConnection ConnectParallel(Action<T1, T2, T3, T4, T5> function);
        public abstract RBXScriptConnection Once(Action<T1, T2, T3, T4, T5> function);
        public abstract object[] Wait();

        public static RBXScriptConnection operator +(RBXScriptSignal<T1, T2, T3, T4, T5> signal, Action<T1, T2, T3, T4, T5> action)
        {
            return signal.Connect(action);
        }
    }

    public abstract class RBXScriptSignal<T1, T2, T3, T4, T5, T6> : IRBXScriptSignal
    {
        public abstract RBXScriptConnection Connect(Action<T1, T2, T3, T4, T5, T6> function);
        public abstract RBXScriptConnection ConnectParallel(Action<T1, T2, T3, T4, T5, T6> function);
        public abstract RBXScriptConnection Once(Action<T1, T2, T3, T4, T5, T6> function);
        public abstract object[] Wait();

        public static RBXScriptConnection operator +(RBXScriptSignal<T1, T2, T3, T4, T5, T6> signal, Action<T1, T2, T3, T4, T5, T6> action)
        {
            return signal.Connect(action);
        }
    }
}
