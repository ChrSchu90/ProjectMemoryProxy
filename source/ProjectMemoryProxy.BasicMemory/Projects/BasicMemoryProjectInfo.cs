namespace ProjectMemoryProxy.BasicMemory.Projects;

using System;

/// <summary>
/// Represents the canonical identity of a local Basic Memory project.
/// </summary>
/// <param name="MemoryProjectId">The Basic Memory external project UUID.</param>
/// <param name="MemoryProjectName">The Basic Memory project name.</param>
public sealed record BasicMemoryProjectInfo(Guid MemoryProjectId, string MemoryProjectName);
