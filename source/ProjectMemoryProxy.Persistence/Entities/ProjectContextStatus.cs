namespace ProjectMemoryProxy.Persistence.Entities;

/// <summary>
/// Project context status.
/// </summary>
public enum ProjectContextStatus
{
    /// <summary>
    /// The project binding is active and can be used for routing.
    /// </summary>
    Active,

    /// <summary>
    /// The binding is inactive and should not be used for routing.
    /// </summary>
    Inactive
}
