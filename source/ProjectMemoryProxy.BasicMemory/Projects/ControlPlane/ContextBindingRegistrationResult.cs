namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;

using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Represents the result of registering a technical context binding.
/// </summary>
/// <param name="Status">The binding registration outcome.</param>
/// <param name="Binding">The binding for successful or idempotent outcomes.</param>
public sealed record ContextBindingRegistrationResult(ContextBindingRegistrationStatus Status, ContextBinding? Binding);
