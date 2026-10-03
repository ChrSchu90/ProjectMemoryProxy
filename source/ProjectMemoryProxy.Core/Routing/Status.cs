namespace ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Status of a routing or binding.
/// </summary>
public enum Status
{
    /// <summary>
    /// The routing or binding is active and can be used.
    /// </summary>
    Active,

    /// <summary>
    /// The routing or binding is inactive and should not be used.
    /// </summary>
    Inactive
}
