
using System.ComponentModel.Design.Serialization;
using System.Diagnostics.Contracts;
using LUSharpTranspiler.Runtime.STL.Enums;
using LUSharpTranspiler.Runtime.STL.Types;

namespace LUSharpTranspiler.Runtime.STL.Classes.Instance
{
    public class Instance : RObject
    {
        /// <summary>
        /// Determines if an Instance and its descendants can be cloned using Instance:Clone(), and can be saved/published.
        /// </summary>
        public bool Archivable { get; set; }
        /// <summary>
        /// The set of capabilities allowed to be used for scripts inside this container.
        /// </summary>
        public SecurityCapability Capabilities { get; set; }

        /// <summary>
        /// A non-unique identifier of the Instance.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Determines the hierarchical parent of the Instance.
        /// </summary>
        public Instance Parent { get; set; }

        /// <summary>
        /// A deprecated property that used to protect CoreGui objects.
        /// </summary>
        public bool RobloxLocked { get; set; }

        /// <summary>
        /// Turns the instance to be a sandboxed container. 
        /// </summary>
        public bool Sandboxed { get; set; }

        // Methods
        public void AddTag(string tag)
        {
            throw new NotImplementedException();
        }

        public void ClearAllChildren()
        {
            throw new NotImplementedException();
        }

        public Instance Clone()
        {
            throw new NotImplementedException();
        }

        public void Destroy()
        {
            throw new NotImplementedException();
        }

        public Instance FindFirstAncestor(string name)
        {
            throw new NotImplementedException();
        }

        public Instance FindFirstAncestorOfClass(string className)
        {
            throw new NotImplementedException();
        }

        public Instance FindFirstAncestorWhichIsA(string className)
        {
            throw new NotImplementedException();
        }

        public Instance FindFirstChild(string name)
        {
            throw new NotImplementedException();
        }

        public Instance FindFirstChildOfClass(string className)
        {
            throw new NotImplementedException();
        }

        public Instance FindFirstChildWhichIsA(string className)
        {
            throw new NotImplementedException();
        }

        public Instance FindFirstDescendant(string name)
        {
            throw new NotImplementedException();
        }

        public Actor GetActor()
        {
            throw new NotImplementedException();
        }

        public object GetAttribute(string attribute)
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal GetAttributeChangedSignal(string attribute)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, object> GetAttributes()
        {
            throw new NotImplementedException();
        }

        public Instance[] GetChildren()
        {
            throw new NotImplementedException();
        }

        public Instance[] GetDescendants()
        {
            throw new NotImplementedException();
        }

        public string GetFullName()
        {
            throw new NotImplementedException();
        }

        public object GetStyled(string name)
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal GetStyledPropertyChangedSignal(string property)
        {
            throw new NotImplementedException();
        }

        public string[] GetTags()
        {
            throw new NotImplementedException();
        }

        public bool HasTag(string tag)
        {
            throw new NotImplementedException();
        }

        public bool IsAncestorOf(Instance descendant)
        {
            throw new NotImplementedException();
        }

        public bool IsDescendantOf(Instance ancestor)
        {
            throw new NotImplementedException();

        }

        public bool IsPropertyModified(string property)
        {
            throw new NotImplementedException();
        }

        public List<Instance> QueryDescendants()
        {
            throw new NotImplementedException();
        }

        public void RemoveTag(string tag)
        {
            throw new NotImplementedException();
        }

        public void ResetPropertyToDefault(string property)
        {
            throw new NotImplementedException();
        }

        public void SetAttribute(string attribute, object value)
        {
            throw new NotImplementedException();
        }

        public Instance WaitForChild(string childName, int timeOut = 0)
        {
            throw new NotImplementedException();
        }

        public static Instance New(string className, RObject? parent = null)
        {
            throw new NotImplementedException();
        }

        public static Instance fromExisting(RObject existingInstance)
        {
            throw new NotImplementedException();
        }

        public static T New<T>(RObject? parent = null) where T : Instance
        {
            throw new NotImplementedException();
        }

        // Events
        public RBXScriptSignal<Instance, Instance> AncestryChanged { get; set; }
        public RBXScriptSignal<string> AttributeChanged { get; set; }
        public RBXScriptSignal<Instance> ChildAdded { get; set; }
        public RBXScriptSignal<Instance> ChildRemoved { get; set; }
        public RBXScriptSignal<Instance> DescendantAdded { get; set; }
        public RBXScriptSignal<Instance> DescendantRemoving { get; set; }
        public RBXScriptSignal Destroying { get; set; }
        public RBXScriptSignal StyledPropertiesChanged { get; set; }
    }
}
