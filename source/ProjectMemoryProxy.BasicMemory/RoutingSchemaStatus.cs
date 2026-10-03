namespace ProjectMemoryProxy.BasicMemory;

/// <summary>
/// Defines the result of analyzing an upstream MCP input schema for routing safety.
/// </summary>
internal enum RoutingSchemaStatus
{
    AutomaticallyRoutable = 0,
    NoProjectSelector,
    RequiredUnsupportedSelector,
    NestedRoutingSelector,
    AlternativeRoutingSelector,
    UnknownRoutingSelector,
    UnsupportedSchema
}
