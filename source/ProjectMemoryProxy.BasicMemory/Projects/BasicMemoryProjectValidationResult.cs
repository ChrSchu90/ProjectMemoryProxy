namespace ProjectMemoryProxy.BasicMemory.Projects;

/// <summary>
/// Represents the result of validating a Basic Memory project identity.
/// </summary>
/// <param name="Status">The validation outcome.</param>
/// <param name="ProjectById">The project found by UUID, when one exists.</param>
/// <param name="ProjectByName">The project found by exact name, when one exists.</param>
public sealed record BasicMemoryProjectValidationResult(
    BasicMemoryProjectValidationStatus Status,
    BasicMemoryProjectInfo? ProjectById,
    BasicMemoryProjectInfo? ProjectByName);
