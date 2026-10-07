namespace ProjectMemoryProxy.BasicMemory.Mirroring;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ProjectMemoryProxy.BasicMemory.Policy;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Converts public mirrored-tool arguments into trusted Basic Memory upstream arguments.
/// </summary>
internal sealed class BasicMemoryInvocationArgumentBuilder
{
    #region Private Fields

    private readonly RoutingManager _routingManager;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryInvocationArgumentBuilder"/> class.
    /// </summary>
    /// <param name="routingManager">The routing manager.</param>
    public BasicMemoryInvocationArgumentBuilder(RoutingManager routingManager)
    {
        _routingManager = routingManager ?? throw new ArgumentNullException(nameof(routingManager));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Builds the Basic Memory upstream arguments for an automatically routed tool invocation.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> BuildAsync(RoutingSchemaAnalysis routingAnalysis, JsonElement publicInputSchema, IEnumerable<KeyValuePair<string, JsonElement>>? publicArguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routingAnalysis);

        if (!routingAnalysis.IsAutomaticallyRoutable)
            throw new ArgumentException("The routing analysis must describe an automatically routable tool.", nameof(routingAnalysis));

        var allowedProperties = GetPublicProperties(publicInputSchema);
        var requiredProperties = GetRequiredProperties(publicInputSchema);
        var arguments = CopyArguments(publicArguments);
        foreach (var argumentName in arguments.Keys)
        {
            if (!allowedProperties.Contains(argumentName))
                throw new McpProtocolException($"Argument '{argumentName}' is not part of the public tool contract.", McpErrorCode.InvalidParams);
        }

        foreach (var requiredProperty in requiredProperties)
        {
            if (!arguments.ContainsKey(requiredProperty))
                throw new McpProtocolException($"Required argument '{requiredProperty}' is missing.", McpErrorCode.InvalidParams);
        }

        if (!arguments.TryGetValue(RoutingPropertyNames.ContextId, out var contextIdElement) || contextIdElement.ValueKind != JsonValueKind.String)
            throw new McpProtocolException($"Argument '{RoutingPropertyNames.ContextId}' must be a string.", McpErrorCode.InvalidParams);

        if (!ContextId.TryParse(contextIdElement.GetString(), out var contextId))
            throw new McpProtocolException($"Argument '{RoutingPropertyNames.ContextId}' is not a valid context identifier.", McpErrorCode.InvalidParams);

        var resolution = await _routingManager.ResolveContextAsync(contextId, cancellationToken).ConfigureAwait(false);
        if (resolution.Status != ContextResolutionStatus.Resolved)
            throw new McpProtocolException($"Argument '{RoutingPropertyNames.ContextId}' does not resolve to an active project routing.", McpErrorCode.InvalidParams);

        if (resolution.MemoryProjectId is not { } memoryProjectId || string.IsNullOrWhiteSpace(resolution.MemoryProjectName))
            throw new InvalidOperationException("A resolved project routing must contain both the Basic Memory project identifier and project name.");

        var upstreamArguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            if (argument.Key == RoutingPropertyNames.ContextId)
                continue;

            upstreamArguments.Add(argument.Key, argument.Value.Clone());
        }

        switch (routingAnalysis.ProjectSelector)
        {
            case ProjectSelectorKind.ProjectId:
                upstreamArguments[RoutingPropertyNames.ProjectId] = memoryProjectId.ToString("D");
                if (routingAnalysis.InjectProjectName)
                {
                    upstreamArguments[RoutingPropertyNames.Project] = resolution.MemoryProjectName;
                }

                break;
            case ProjectSelectorKind.ProjectName:
                upstreamArguments[RoutingPropertyNames.Project] = resolution.MemoryProjectName;
                break;
            default:
                throw new InvalidOperationException("An automatically routable tool must use a supported project selector.");
        }

        return upstreamArguments;
    }

    #endregion

    #region Private Methods

    private static HashSet<string> GetPublicProperties(JsonElement publicInputSchema)
    {
        if (publicInputSchema.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The public input schema must be a JSON object.", nameof(publicInputSchema));

        if (!publicInputSchema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The public input schema must define object properties.", nameof(publicInputSchema));

        if (!publicInputSchema.TryGetProperty("additionalProperties", out var additionalProperties) || additionalProperties.ValueKind != JsonValueKind.False)
            throw new ArgumentException("The public input schema must reject additional properties.", nameof(publicInputSchema));

        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties.EnumerateObject())
        {
            propertyNames.Add(property.Name);
        }

        if (!propertyNames.Contains(RoutingPropertyNames.ContextId))
            throw new ArgumentException($"The public input schema must expose '{RoutingPropertyNames.ContextId}'.", nameof(publicInputSchema));

        return propertyNames;
    }

    private static HashSet<string> GetRequiredProperties(JsonElement publicInputSchema)
    {
        if (!publicInputSchema.TryGetProperty("required", out var required) || required.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("The public input schema must define required properties.", nameof(publicInputSchema));

        var requiredProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requiredProperty in required.EnumerateArray())
        {
            if (requiredProperty.ValueKind != JsonValueKind.String)
                throw new ArgumentException("The public input schema contains an invalid required-property entry.", nameof(publicInputSchema));

            var propertyName = requiredProperty.GetString();
            if (propertyName == null)
                throw new ArgumentException("The public input schema contains an invalid required-property entry.", nameof(publicInputSchema));

            requiredProperties.Add(propertyName);
        }

        return requiredProperties;
    }

    private static Dictionary<string, JsonElement> CopyArguments(IEnumerable<KeyValuePair<string, JsonElement>>? publicArguments)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (publicArguments == null)
            return arguments;

        foreach (var argument in publicArguments)
        {
            if (!arguments.TryAdd(argument.Key, argument.Value.Clone()))
            {
                throw new McpProtocolException($"Argument '{argument.Key}' was supplied more than once.", McpErrorCode.InvalidParams);
            }
        }

        return arguments;
    }


    #endregion
}
