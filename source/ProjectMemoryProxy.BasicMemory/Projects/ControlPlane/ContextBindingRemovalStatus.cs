namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;

/// <summary>
/// Describes the outcome of removing a context binding.
/// </summary>
public enum ContextBindingRemovalStatus
{
    /// <summary>
    /// The context binding was removed successfully.
    /// </summary>
    Unbound,

    /// <summary>
    /// No binding exists for the requested context.
    /// </summary>
    AlreadyUnbound,

    /// <summary>
    /// Concurrent registry activity prevented the removal from being classified safely.
    /// </summary>
    RegistryWriteConflict
}
