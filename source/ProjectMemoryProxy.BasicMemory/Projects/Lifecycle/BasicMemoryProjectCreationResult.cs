namespace ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;

using ProjectMemoryProxy.BasicMemory.Projects.Directory;

/// <summary>
/// Represents the normalized result of a Basic Memory project creation operation.
/// </summary>
/// <param name="Status">The creation outcome.</param>
/// <param name="Project">The project identity returned by Basic Memory.</param>
/// <param name="ProjectPath">The project path returned by Basic Memory.</param>
public sealed record BasicMemoryProjectCreationResult(BasicMemoryProjectCreationStatus Status, BasicMemoryProjectInfo Project, string ProjectPath);
