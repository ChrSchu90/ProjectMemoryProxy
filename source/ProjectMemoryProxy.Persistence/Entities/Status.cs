namespace ProjectMemoryProxy.Persistence.Entities;

/// <summary>
/// Status of a project or binding.
/// </summary>
public enum Status
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
