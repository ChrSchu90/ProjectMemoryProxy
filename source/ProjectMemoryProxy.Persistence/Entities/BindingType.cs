namespace ProjectMemoryProxy.Persistence.Entities;

using System;

/// <summary>
/// Context 
/// </summary>
public enum BindingType
{
    /// <summary>
    /// A ChatGPT project (<c>chatgpt-project</c>)
    /// </summary>
    ChatGptProject,

    /// <summary>
    /// A Git repository (<c>git</c>)
    /// </summary>
    GitRepository
}

/// <summary>
/// Helper methods for the <see cref="BindingType"/> enum.
/// </summary>
internal static class BindingTypeHelper
{
    internal static string ToIdentifier(BindingType contextType)
    {
        return contextType switch
        {
            BindingType.ChatGptProject => "chatgpt-project",
            BindingType.GitRepository => "git",
            _ => throw new ArgumentOutOfRangeException(nameof(contextType), contextType, null)
        };
    }

    internal static BindingType FromIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return identifier.Trim().ToLowerInvariant() switch
            {
                "chatgpt-project" => BindingType.ChatGptProject,
                "git" => BindingType.GitRepository,
                _ => throw new ArgumentOutOfRangeException(nameof(identifier), identifier, null)
            };
    }
}
