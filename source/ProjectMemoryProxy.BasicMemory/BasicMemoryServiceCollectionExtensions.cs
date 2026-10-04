namespace ProjectMemoryProxy.BasicMemory;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;
using ProjectMemoryProxy.BasicMemory.MCP;
using ProjectMemoryProxy.BasicMemory.Mirroring;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Provides Basic Memory service registrations.
/// </summary>
public static class BasicMemoryServiceCollectionExtensions
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Registers the Basic Memory MCP upstream integration.
    /// </summary>
    public static IServiceCollection AddBasicMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IBasicMemoryClient, BasicMemoryClient>();
        services.AddSingleton<BasicMemoryToolCatalog>();
        services.AddSingleton<IBasicMemoryProjectDirectory, BasicMemoryProjectDirectory>();
        services.AddSingleton<IBasicMemoryProjectLifecycle, BasicMemoryProjectLifecycle>();
        services.AddSingleton<ProjectRegistryManager>();
        services.AddSingleton<BasicMemoryMirroredToolCatalog>();
        services.AddSingleton<BasicMemoryMirroredMcpServerToolRegistry>();
        services.AddSingleton<IPostConfigureOptions<McpServerOptions>, BasicMemoryMcpServerOptionsSetup>();
        return services;
    }

    /// <summary>
    /// Discovers safely mirrorable Basic Memory tools and registers them with the MCP server.
    /// </summary>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task RegisterBasicMemoryMirroredToolsAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var serverToolRegistry = serviceProvider.GetRequiredService<BasicMemoryMirroredMcpServerToolRegistry>();
        if (serverToolRegistry.IsInitialized)
            return;
        
        var mirroredToolCatalog = serviceProvider.GetRequiredService<BasicMemoryMirroredToolCatalog>();
        await mirroredToolCatalog.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var routingManager = serviceProvider.GetRequiredService<RoutingManager>();
        var argumentBuilder = new BasicMemoryInvocationArgumentBuilder(routingManager);

        // Resolve the cached options before publishing the mirrored-tool registry. This gives us the
        // proxy-owned/static tool set for collision detection while the mirrored registry is still empty.
        var serverOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var toolCollection = serverOptions.ToolCollection ??= new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal);
        var mirroredServerTools = new List<McpServerTool>(mirroredToolCatalog.Tools.Count);
        foreach (var mirroredTool in mirroredToolCatalog.Tools)
        {
            if (toolCollection.TryGetPrimitive(mirroredTool.Name, out _))
                throw new InvalidOperationException($"Cannot register mirrored Basic Memory MCP tool '{mirroredTool.Name}' because another MCP tool with the same name is already registered.");

            mirroredServerTools.Add(new BasicMemoryMirroredMcpServerTool(mirroredTool, argumentBuilder));
        }

        serverToolRegistry.Initialize(mirroredServerTools);

        // Keep the cached options instance in sync for stateful/session-based servers and HTTP-layer validation. Stateless Streamable HTTP creates
        // fresh McpServerOptions instances per request; BasicMemoryMcpServerOptionsSetup adds the same registry snapshot to those instances.
        using (toolCollection.DeferChangedEvents())
        {
            foreach (var serverTool in mirroredServerTools)
            {
                toolCollection.Add(serverTool);
            }
        }
    }

    #endregion

    #region Private Methods

    #endregion
}
