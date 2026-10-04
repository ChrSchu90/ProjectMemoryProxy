namespace ProjectMemoryProxy.BasicMemory.Mirroring;

using System;
using System.Text.Json;
using ModelContextProtocol.Client;
using ProjectMemoryProxy.BasicMemory.Policy;

/// <summary>
/// Represents a Basic Memory tool exposed through the ProjectMemoryProxy MCP surface.
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
    public BasicMemoryMirroredTool(McpClientTool upstreamTool, ToolRoutingClassification classification, RoutingSchemaAnalysis routingAnalysis, JsonElement publicInputSchema)
    {
        UpstreamTool = upstreamTool ?? throw new ArgumentNullException(nameof(upstreamTool));
        RoutingAnalysis = routingAnalysis ?? throw new ArgumentNullException(nameof(routingAnalysis));

        if (classification != ToolRoutingClassification.AutomaticallyRouted && classification != ToolRoutingClassification.GlobalAllowlisted)
            throw new ArgumentException("The classification must describe an exposed proxy tool.", nameof(classification));

        if (classification == ToolRoutingClassification.AutomaticallyRouted && !routingAnalysis.IsAutomaticallyRoutable)
            throw new ArgumentException("An automatically routed tool must have an automatically routable schema.", nameof(routingAnalysis));


        if (classification == ToolRoutingClassification.GlobalAllowlisted && routingAnalysis.Status != RoutingSchemaStatus.NoProjectSelector)
            throw new ArgumentException("A global allowlisted tool must not expose project-routing semantics.", nameof(routingAnalysis));


        if (publicInputSchema.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The public input schema must be a JSON object.", nameof(publicInputSchema));

        Classification = classification;
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
    /// Gets how the upstream tool is exposed by ProjectMemoryProxy.
    /// </summary>
    public ToolRoutingClassification Classification { get; }

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
