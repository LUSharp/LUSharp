
namespace LUSharpAPI.Runtime.STL.Enums
{
    public enum ModelStreamingMode
    {
        /// <summary>
        /// Engine determines best behavior. Currently equivalent to Nonatomic.
        /// </summary>
        Default,
        /// <summary>
        /// The Model and all of its descendants are streamed in/out together. For streaming in, this applies when any descendant BasePart is eligible for streaming in. For streaming out, this applies when all descendant BaseParts are eligible for streaming out.
        /// </summary>
        Atomic,
        /// <summary>
        /// Persistent models are sent as a complete atomic unit soon after the player joins and before the Workspace.PersistentLoaded event fires. Persistent models and their descendants are never streamed out.
        /// </summary>
        Persistent,
        /// <summary>
        /// Behaves as a persistent model for players that have been added using Model:AddPersistentPlayer(). For other players, behavior is the same as Atomic. You can revert a model from player persistence via Model:RemovePersistentPlayer().
        /// </summary>
        PersistentPerPlayer,
        /// <summary>
        /// When a nonatomic model is streamed, descendants are also sent, except for part descendants. Nonatomic models that are not descendants of parts are sent during experience loading.
        /// </summary>
        Nonatomic
    }
}
