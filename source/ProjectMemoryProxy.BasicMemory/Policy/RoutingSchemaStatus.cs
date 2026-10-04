namespace ProjectMemoryProxy.BasicMemory.Policy;

/// <summary>
/// Defines the result of analyzing an upstream MCP input schema for server-controlled project routing.
/// </summary>
internal enum RoutingSchemaStatus
{
    /// <summary>
    /// The schema exposes only supported local project-routing selectors and can be routed generically by the proxy.
    /// </summary>
    AutomaticallyRoutable = 0,

    /// <summary>
    /// The schema does not expose a supported local <c>project</c> or <c>project_id</c> selector.
    /// </summary>
    NoProjectSelector,

    /// <summary>
    /// The schema requires an unsupported routing selector such as a cloud workspace, tenant, or cross-project selector.
    /// </summary>
    RequiredUnsupportedSelector,

    /// <summary>
    /// The schema contains routing-sensitive properties below the top-level tool argument object.
    /// </summary>
    NestedRoutingSelector,

    /// <summary>
    /// The schema expresses routing-sensitive behavior through an alternative or conditional schema branch that cannot be controlled generically.
    /// </summary>
    AlternativeRoutingSelector,

    /// <summary>
    /// The schema contains a routing-like property whose semantics are not part of the supported canonical selector set.
    /// </summary>
    UnknownRoutingSelector,

    /// <summary>
    /// The schema uses malformed, unresolved, cyclic, or otherwise unsupported JSON Schema constructs that prevent safe routing analysis.
    /// </summary>
    UnsupportedSchema
}
