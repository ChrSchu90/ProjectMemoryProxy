namespace ProjectMemoryProxy.BasicMemory;

/// <summary>
/// Defines the Basic Memory project selector used for a generic upstream invocation.
/// </summary>
internal enum ProjectSelectorKind
{
    /// <summary>
    /// No supported local project selector is available.
    /// </summary>
    None = 0,

    /// <summary>
    /// The upstream tool accepts the Basic Memory project external identifier.
    /// </summary>
    ProjectId,

    /// <summary>
    /// The upstream tool accepts the local Basic Memory project name.
    /// </summary>
    ProjectName
}
