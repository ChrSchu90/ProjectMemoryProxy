namespace ProjectMemoryProxy.BasicMemory.Projects;

/// <summary>
/// Describes the outcome of changing a context-binding status.
/// </summary>
public enum ContextBindingStatusChangeStatus
{
    /// <summary>
    /// The context-binding status was updated successfully.
    /// </summary>
    Updated,

    /// <summary>
    /// The context binding was already in the requested state.
    /// </summary>
    AlreadyInRequestedState,

    /// <summary>
    /// No binding exists for the requested context.
    /// </summary>
    ContextBindingNotFound,

    /// <summary>
    /// The binding refers to a project routing that no longer exists.
    /// </summary>
    ProjectRoutingNotFound,

    /// <summary>
    /// The binding cannot be activated because its target project routing is inactive.
    /// </summary>
    ProjectRoutingInactive,

    /// <summary>
    /// Concurrent registry activity prevented the requested transition from being classified safely.
    /// </summary>
    RegistryWriteConflict
}
