namespace ProjectMemoryProxy.BasicMemory.Projects;

/// <summary>
/// Describes the result of validating a stored Basic Memory project identity.
/// </summary>
public enum BasicMemoryProjectValidationStatus
{
    /// <summary>
    /// The expected project UUID and name identify the same Basic Memory project.
    /// </summary>
    ExactMatch,

    /// <summary>
    /// Neither the expected UUID nor the expected name exists in Basic Memory.
    /// </summary>
    NotFound,

    /// <summary>
    /// The expected UUID exists, but it belongs to a project with a different name.
    /// </summary>
    NameMismatch,

    /// <summary>
    /// The expected name exists, but it belongs to a project with a different UUID.
    /// </summary>
    IdMismatch,

    /// <summary>
    /// The expected UUID and expected name both exist, but identify different projects.
    /// </summary>
    IdentityConflict
}
