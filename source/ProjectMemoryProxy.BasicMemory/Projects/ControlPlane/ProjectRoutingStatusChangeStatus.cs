namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;

/// <summary>
/// Describes the outcome of changing a registered project-routing status.
/// </summary>
public enum ProjectRoutingStatusChangeStatus
{
    /// <summary>
    /// The project-routing status was updated successfully.
    /// </summary>
    Updated,

    /// <summary>
    /// The project routing was already in the requested state.
    /// </summary>
    AlreadyInRequestedState,

    /// <summary>
    /// No project routing exists for the requested Basic Memory project.
    /// </summary>
    ProjectRoutingNotFound,

    /// <summary>
    /// The Basic Memory project no longer exists.
    /// </summary>
    BasicMemoryProjectNotFound,

    /// <summary>
    /// The persisted project name no longer matches the Basic Memory project identifier.
    /// </summary>
    BasicMemoryProjectNameMismatch,

    /// <summary>
    /// The persisted project identifier no longer matches the Basic Memory project name.
    /// </summary>
    BasicMemoryProjectIdMismatch,

    /// <summary>
    /// The persisted Basic Memory project identity resolves to conflicting projects.
    /// </summary>
    BasicMemoryProjectIdentityConflict,

    /// <summary>
    /// Basic Memory returned an unsupported validation result and activation failed closed.
    /// </summary>
    BasicMemoryValidationFailed,

    /// <summary>
    /// Concurrent registry activity prevented the requested transition from being classified safely.
    /// </summary>
    RegistryWriteConflict
}
