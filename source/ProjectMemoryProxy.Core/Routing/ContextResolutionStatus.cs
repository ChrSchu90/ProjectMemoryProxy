namespace ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Describes the outcome of resolving a context to a memory project.
/// </summary>
public enum ContextResolutionStatus
{
    /// <summary>
    /// The context was resolved to an active memory project routing.
    /// </summary>
    Resolved,

    /// <summary>
    /// No binding exists for the context.
    /// </summary>
    NotBound,

    /// <summary>
    /// The target project routing is inactive.
    /// </summary>
    ProjectRoutingInactive,

    /// <summary>
    /// The context binding is inactive.
    /// </summary>
    BindingInactive
}
