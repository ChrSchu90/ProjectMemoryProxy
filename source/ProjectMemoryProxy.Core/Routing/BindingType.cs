namespace ProjectMemoryProxy.Core.Routing;

using System;


/// <summary>
/// Identifies the source type of project context binding.
/// </summary>
public enum BindingType
{
    /// <summary>
    /// A ChatGPT project context using the <c>chatgpt-project</c> identifier.
    /// </summary>
    ChatGptProject,

    /// <summary>
    /// A Git repository context using the <c>git</c> identifier.
    /// </summary>
    GitRepository
}
