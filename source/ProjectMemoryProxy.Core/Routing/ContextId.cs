namespace ProjectMemoryProxy.Core.Routing;

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents a canonical technical context identifier used for project routing.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public sealed record ContextId
{
    #region Static Fields

    /// <summary>
    /// The ChatGPT project identifier
    /// </summary>
    internal const string ChatGptProjectIdentifier = "chatgpt-project";

    /// <summary>
    /// The Git repository identifier
    /// </summary>
    internal const string GitRepositoryIdentifier = "git";

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextId"/> class.
    /// </summary>
    /// <param name="bindingType">Type of the binding.</param>
    /// <param name="bindingName">Name of the binding.</param>
    private ContextId(BindingType bindingType, string bindingName)
    {
        BindingType = bindingType;
        BindingName = bindingName;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the binding type.
    /// </summary>
    public BindingType BindingType { get; }

    /// <summary>
    /// Gets the opaque binding-specific identifier.
    /// </summary>
    public string BindingName { get; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Attempts to parse a canonical technical context identifier.
    /// </summary>
    /// <param name="value">The context identifier.</param>
    /// <param name="contextId">The parsed context identifier when successful.</param>
    /// <returns><see langword="true"/> when the identifier is valid; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out ContextId? contextId)
    {
        contextId = null;

        if (string.IsNullOrEmpty(value))
            return false;

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
            return false;

        var separatorIndex = value.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            return false;

        var typeIdentifier = value[..separatorIndex];
        var bindingName = value[(separatorIndex + 1)..];

        if (!TryParseBindingType(typeIdentifier, out var bindingType))
            return false;

        contextId = new ContextId(bindingType, bindingName);
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{GetBindingTypeIdentifier(BindingType)}:{BindingName}";
    }

    #endregion

    #region Private Methods

    private static bool TryParseBindingType(string identifier, out BindingType bindingType)
    {
        switch (identifier)
        {
            case ChatGptProjectIdentifier:
                bindingType = BindingType.ChatGptProject;
                return true;

            case GitRepositoryIdentifier:
                bindingType = BindingType.GitRepository;
                return true;

            default:
                bindingType = default;
                return false;
        }
    }

    private static string GetBindingTypeIdentifier(BindingType bindingType)
    {
        return bindingType switch
        {
            BindingType.ChatGptProject => ChatGptProjectIdentifier,
            BindingType.GitRepository => GitRepositoryIdentifier,
            _ => throw new ArgumentOutOfRangeException(nameof(bindingType), bindingType, null)
        };
    }

    #endregion
}
