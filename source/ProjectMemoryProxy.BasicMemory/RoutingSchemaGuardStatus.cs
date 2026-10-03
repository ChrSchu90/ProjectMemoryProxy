namespace ProjectMemoryProxy.BasicMemory;

/// <summary>
/// Describes whether an upstream input schema matches an approved routing contract.
/// </summary>
internal enum RoutingSchemaGuardStatus
{
    /// <summary>
    /// The schema cannot be interpreted safely enough for automatic routing.
    /// </summary>
    UnsupportedSchema = 0,

    /// <summary>
    /// The schema contains routing selectors outside the approved contract.
    /// </summary>
    UnexpectedRoutingSelector,

    /// <summary>
    /// The schema matches the approved routing contract.
    /// </summary>
    Compatible
}
