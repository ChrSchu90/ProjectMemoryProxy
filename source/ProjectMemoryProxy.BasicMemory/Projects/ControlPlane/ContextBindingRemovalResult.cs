namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;
/// <summary>
/// Represents the result of removing a context binding.
/// </summary>
/// <param name="Status">The binding-removal outcome.</param>
public sealed record ContextBindingRemovalResult(ContextBindingRemovalStatus Status);
