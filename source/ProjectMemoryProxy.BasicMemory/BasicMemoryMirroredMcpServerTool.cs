namespace ProjectMemoryProxy.BasicMemory;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Exposes one mirrored Basic Memory tool through the ProjectMemoryProxy MCP server.
/// </summary>
internal sealed class BasicMemoryMirroredMcpServerTool : McpServerTool
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly BasicMemoryInvocationArgumentBuilder _argumentBuilder;
    private readonly BasicMemoryMirroredTool _mirroredTool;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryMirroredMcpServerTool"/> class.
    /// </summary>
    public BasicMemoryMirroredMcpServerTool(BasicMemoryMirroredTool mirroredTool, BasicMemoryInvocationArgumentBuilder argumentBuilder)
    {
        _mirroredTool = mirroredTool ?? throw new ArgumentNullException(nameof(mirroredTool));
        _argumentBuilder = argumentBuilder ?? throw new ArgumentNullException(nameof(argumentBuilder));
        ProtocolTool = CreateProtocolTool(mirroredTool);
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public override Tool ProtocolTool { get; }

    /// <inheritdoc />
    public override IReadOnlyList<object> Metadata { get; } = Array.Empty<object>();

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyDictionary<string, object?> upstreamArguments;
        switch (_mirroredTool.Classification)
        {
            case ToolRoutingClassification.AutomaticallyRouted:
                upstreamArguments = await _argumentBuilder.BuildAsync(_mirroredTool.RoutingAnalysis, _mirroredTool.PublicInputSchema, request.Params.Arguments, cancellationToken).ConfigureAwait(false);
                break;
            case ToolRoutingClassification.GlobalAllowlisted:
                upstreamArguments = CopyArguments(request.Params.Arguments);
                break;
            default:
                throw new InvalidOperationException($"Mirrored tool '{_mirroredTool.Name}' has unsupported exposure classification '{_mirroredTool.Classification}'.");
        }

        return await _mirroredTool.UpstreamTool.CallAsync(upstreamArguments, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Private Methods

    private static Tool CreateProtocolTool(BasicMemoryMirroredTool mirroredTool)
    {
        var upstreamTool = mirroredTool.UpstreamTool.ProtocolTool;
        return new Tool
        {
            Name = upstreamTool.Name,
            Title = upstreamTool.Title,
            Description = upstreamTool.Description,
            InputSchema = mirroredTool.PublicInputSchema.Clone(),
            OutputSchema = upstreamTool.OutputSchema?.Clone(),
            Annotations = CloneAnnotations(upstreamTool.Annotations),
            Icons = upstreamTool.Icons == null ? null : new List<Icon>(upstreamTool.Icons),
            Meta = upstreamTool.Meta == null ? null : (JsonObject)upstreamTool.Meta.DeepClone()
        };
    }

    private static ToolAnnotations? CloneAnnotations(ToolAnnotations? annotations)
    {
        if (annotations == null)
            return null;

        return new ToolAnnotations
        {
            Title = annotations.Title,
            DestructiveHint = annotations.DestructiveHint,
            IdempotentHint = annotations.IdempotentHint,
            OpenWorldHint = annotations.OpenWorldHint,
            ReadOnlyHint = annotations.ReadOnlyHint
        };
    }

    private static IReadOnlyDictionary<string, object?> CopyArguments(IEnumerable<KeyValuePair<string, JsonElement>>? arguments)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (arguments == null)
            return result;

        foreach (var argument in arguments)
        {
            result.Add(argument.Key, argument.Value.Clone());
        }

        return result;
    }

    #endregion
}
