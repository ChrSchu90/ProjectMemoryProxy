namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;

/// <summary>
/// Describes the outcome of registering a context binding.
/// </summary>
public enum ContextBindingRegistrationStatus
{
    /// <summary>
    /// The context binding was created successfully.
    /// </summary>
    Bound,

    /// <summary>
    /// The exact context was already bound to the requested project.
    /// </summary>
    AlreadyBound,

    /// <summary>
    /// No registered project routing exists for the requested Basic Memory project.
    /// </summary>
    ProjectRoutingNotFound,

    /// <summary>
    /// The requested project routing exists but is inactive.
    /// </summary>
    ProjectRoutingInactive,

    /// <summary>
    /// The context is already bound to a different project.
    /// </summary>
    ContextAlreadyBoundToDifferentProject,

    /// <summary>
    /// A concurrent binding write conflicted and the resulting state could not be classified safely.
    /// </summary>
    RegistryWriteConflict
}
