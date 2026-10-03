namespace ProjectMemoryProxy.BasicMemory;

/// <summary>
/// Defines how a discovered Basic Memory tool may participate in proxy routing.
/// </summary>
internal enum ToolRoutingClassification
{
    /// <summary>
    /// The tool must not be exposed through the generic proxy path.
    /// </summary>
    Blocked = 0,

    /// <summary>
    /// The tool is intentionally excluded from proxy exposure by the current proxy scope.
    /// </summary>
    IntentionallyBlocked,

    /// <summary>
    /// The tool can use generic server-controlled project routing.
    /// </summary>
    AutomaticallyRouted,

    /// <summary>
    /// The tool requires dedicated proxy behavior instead of generic routing.
    /// </summary>
    ExplicitAdapter,

    /// <summary>
    /// The tool manages Basic Memory project lifecycle and must not be transparently proxied.
    /// </summary>
    ProjectLifecycle,

    /// <summary>
    /// The tool is explicitly approved as a global operation.
    /// </summary>
    GlobalAllowlisted
}
