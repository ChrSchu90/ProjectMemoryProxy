namespace ProjectMemoryProxy.BasicMemory;

using ModelContextProtocol.Client;
using System;
using System.Text.Json;

/// <summary>
/// Represents a Basic Memory tool that can be exposed through the generic server-controlled proxy path.
/// </summary>
internal sealed class BasicMemoryMirroredTool
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryMirroredTool"/> class.
    /// </summary>
    public BasicMemoryMirroredTool(McpClientTool upstreamTool, RoutingSchemaAnalysis routingAnalysis, JsonElement publicInputSchema)
    {
        UpstreamTool = upstreamTool ?? throw new ArgumentNullException(nameof(upstreamTool));
        RoutingAnalysis = routingAnalysis ?? throw new ArgumentNullException(nameof(routingAnalysis));

        if (!routingAnalysis.IsAutomaticallyRoutable)
            throw new ArgumentException("The routing analysis must describe an automatically routable tool.", nameof(routingAnalysis));

        if (publicInputSchema.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The public input schema must be a JSON object.", nameof(publicInputSchema));

        PublicInputSchema = publicInputSchema.Clone();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the canonical upstream MCP tool.
    /// </summary>
    public McpClientTool UpstreamTool { get; }

    /// <summary>
    /// Gets the canonical upstream MCP tool name.
    /// </summary>
    public string Name => UpstreamTool.ProtocolTool.Name;

    /// <summary>
    /// Gets the routing analysis used for server-controlled invocation.
    /// </summary>
    public RoutingSchemaAnalysis RoutingAnalysis { get; }

    /// <summary>
    /// Gets the rewritten input schema that may be exposed by ProjectMemoryProxy.
    /// </summary>
    public JsonElement PublicInputSchema { get; }

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion
}
