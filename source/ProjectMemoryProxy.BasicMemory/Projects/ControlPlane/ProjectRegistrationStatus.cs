namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;

/// <summary>
/// Describes the outcome of registering an existing Basic Memory project with ProjectMemoryProxy.
/// </summary>
public enum ProjectRegistrationStatus
{
    /// <summary>
    /// The project routing was registered successfully.
    /// </summary>
    Registered,

    /// <summary>
    /// The exact project routing was already registered and no persistence change was required.
    /// </summary>
    AlreadyRegistered,

    /// <summary>
    /// The expected Basic Memory project does not exist.
    /// </summary>
    BasicMemoryProjectNotFound,

    /// <summary>
    /// The expected Basic Memory project identifier exists under a different project name.
    /// </summary>
    BasicMemoryProjectNameMismatch,

    /// <summary>
    /// The expected Basic Memory project name exists under a different project identifier.
    /// </summary>
    BasicMemoryProjectIdMismatch,

    /// <summary>
    /// The expected Basic Memory project identifier and name resolve to different projects.
    /// </summary>
    BasicMemoryProjectIdentityConflict,

    /// <summary>
    /// Basic Memory returned an unsupported validation outcome and registration failed closed.
    /// </summary>
    BasicMemoryValidationFailed,

    /// <summary>
    /// The requested Basic Memory project identifier is already registered under a different name.
    /// </summary>
    RegistryProjectNameMismatch,

    /// <summary>
    /// The requested Basic Memory project name is already registered under a different identifier.
    /// </summary>
    RegistryProjectIdMismatch,

    /// <summary>
    /// The requested Basic Memory project identifier and name are associated with different registry entries.
    /// </summary>
    RegistryProjectIdentityConflict,

    /// <summary>
    /// A concurrent registry write conflicted and the resulting persisted state could not be classified safely.
    /// </summary>
    RegistryWriteConflict
}
