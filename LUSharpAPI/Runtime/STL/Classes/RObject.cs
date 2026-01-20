
using LUSharpAPI.Runtime.STL.Types;

namespace LUSharpAPI.Runtime.STL.Classes
{
    public class RObject
    {
        /// <summary>
        /// Creates a new <see cref="Instance"/> of type className. Abstract classes and services cannot be created with this constructor.
        /// Note that when the Parent of an object is set, Luau listens to a variety of different property changes for replication, rendering, and physics.
        /// For performance reasons, it's recommended to set the instance's Parent property last when creating new objects, instead of specifying the second argument(parent) of this constructor.
        /// </summary>
        /// <param name="className">Class name of the new instance to create.</param>
        /// <param name="parent">Optional object to parent the new instance to. Not recommended for performance reasons (see description above).</param>
        private static RObject New(string className, RObject? parent = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new object with the same type and property values as an existing object. In most cases using Instance:Clone() is more appropriate, but this constructor is useful when implementing low‑level libraries or systems.
        /// There are two behavioral differences between this constructor and the Instance:Clone() method:
        /// This constructor will not copy any of the descendant Instances parented to the existing object.
        /// This constructor will return a new object even if the existing object had Instance.Archivable set to false.
        /// </summary>
        /// <param name="existingInstance">The existing <see cref="Instance"/> to copy property values from.</param>
        /// <returns><see cref="Instance"/></returns>
        private static RObject fromExisting(RObject existingInstance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A read-only string representing the class this Object belongs to.
        /// </summary>
        public string ClassName { get; internal set; }

        /// <summary>
        /// Get an event that fires when a given property of the object changes.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public RBXScriptSignal<string> GetPropertyChangedSignal { get; set; }

        /// <summary>
        /// Returns true if an object's class matches or inherits from a given class.
        /// </summary>
        /// <param name="className"></param>
        /// <returns></returns>
        public bool IsA(string className)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Fired immediately after a property of the object changes, with some limitations.
        /// </summary>
        /// <param name="className"></param>
        /// <returns></returns>
        public RBXScriptSignal<string> Changed { get; set; }
    }
}