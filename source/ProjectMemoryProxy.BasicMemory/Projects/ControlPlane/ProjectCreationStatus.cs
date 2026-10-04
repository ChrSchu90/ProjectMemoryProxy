namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;

/// <summary>
/// Describes the outcome of creating and registering a Basic Memory project.
/// </summary>
public enum ProjectCreationStatus
{
    /// <summary>
    /// Basic Memory created the project and ProjectMemoryProxy registered its routing.
    /// </summary>
    Created,

    /// <summary>
    /// An existing Basic Memory project from an earlier or concurrent create was registered successfully.
    /// </summary>
    Recovered,

    /// <summary>
    /// The exact project was already registered and no mutation was required.
    /// </summary>
    AlreadyRegistered,

    /// <summary>
    /// Basic Memory returned a project name that does not exactly match the requested name.
    /// </summary>
    BasicMemoryProjectNameMismatch,

    /// <summary>
    /// An unregistered existing Basic Memory project uses the requested name at a different filesystem path.
    /// </summary>
    BasicMemoryProjectPathMismatch,

    /// <summary>
    /// The expected Basic Memory project could not be found after creation.
    /// </summary>
    BasicMemoryProjectNotFound,

    /// <summary>
    /// The expected Basic Memory project name resolves to a different project identifier.
    /// </summary>
    BasicMemoryProjectIdMismatch,

    /// <summary>
    /// The expected Basic Memory project identifier and name resolve to different projects.
    /// </summary>
    BasicMemoryProjectIdentityConflict,

    /// <summary>
    /// Basic Memory returned an unsupported validation outcome.
    /// </summary>
    BasicMemoryValidationFailed,

    /// <summary>
    /// The Basic Memory project identifier is already registered under another name.
    /// </summary>
    RegistryProjectNameMismatch,

    /// <summary>
    /// The Basic Memory project name is already registered under another identifier.
    /// </summary>
    RegistryProjectIdMismatch,

    /// <summary>
    /// The Basic Memory project identifier and name conflict with different registry records.
    /// </summary>
    RegistryProjectIdentityConflict,

    /// <summary>
    /// A concurrent registry write could not be classified safely.
    /// </summary>
    RegistryWriteConflict
}
