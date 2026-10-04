namespace ProjectMemoryProxy.BasicMemory.Projects;

/// <summary>
/// Describes the outcome of deleting a Basic Memory project and its ProjectMemoryProxy routing.
/// </summary>
public enum ProjectDeletionStatus
{
    /// <summary>
    /// The Basic Memory project and the registered ProjectMemoryProxy routing were deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// Basic Memory no longer contained the project and a stale ProjectMemoryProxy routing was removed.
    /// </summary>
    Recovered,

    /// <summary>
    /// No ProjectMemoryProxy routing exists for the requested Basic Memory project identifier.
    /// </summary>
    AlreadyDeleted,

    /// <summary>
    /// The expected Basic Memory project identifier exists under a different name.
    /// </summary>
    BasicMemoryProjectNameMismatch,

    /// <summary>
    /// The expected Basic Memory project name exists under a different identifier.
    /// </summary>
    BasicMemoryProjectIdMismatch,

    /// <summary>
    /// The expected Basic Memory project identifier and name resolve to different projects.
    /// </summary>
    BasicMemoryProjectIdentityConflict,

    /// <summary>
    /// Basic Memory still contains the exact project after the delete operation completed.
    /// </summary>
    BasicMemoryDeleteNotCompleted,

    /// <summary>
    /// Basic Memory returned an unsupported validation outcome.
    /// </summary>
    BasicMemoryValidationFailed,

    /// <summary>
    /// The local routing could not be removed or its concurrent state could not be classified safely.
    /// </summary>
    RegistryWriteConflict
}
