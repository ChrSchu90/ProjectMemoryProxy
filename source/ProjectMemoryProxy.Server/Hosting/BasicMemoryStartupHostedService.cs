namespace ProjectMemoryProxy.Server.Hosting;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectMemoryProxy.BasicMemory;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Blocks application startup until Basic Memory is reachable and its proxy toolset is registered.
/// </summary>
internal sealed class BasicMemoryStartupHostedService : IHostedService
{
    #region Static Fields

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    #endregion

    #region Private Fields

    private readonly ILogger<BasicMemoryStartupHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryStartupHostedService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <param name="logger">The logger.</param>
    public BasicMemoryStartupHostedService(IServiceProvider serviceProvider, ILogger<BasicMemoryStartupHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waiting for Basic Memory MCP to be available and expose its toolset...");
        await BasicMemoryStartupRetryPolicy.ExecuteAsync(_serviceProvider.RegisterBasicMemoryMirroredToolsAsync, _logger, RetryDelay, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Received Basic Memory MCP toolset and registered mirrored tools.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Private Methods

    #endregion
}
