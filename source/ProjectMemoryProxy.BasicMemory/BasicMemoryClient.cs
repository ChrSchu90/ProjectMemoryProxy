namespace ProjectMemoryProxy.BasicMemory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ProjectMemoryProxy.Core.Configuration;

/// <summary>
/// Provides MCP access to the configured Basic Memory upstream.
/// </summary>
internal sealed class BasicMemoryClient : IBasicMemoryClient, IAsyncDisposable
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly ProjectMemoryProxyOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BasicMemoryClient> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private McpClient? _client;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryClient"/> class.
    /// </summary>
    public BasicMemoryClient(IOptions<ProjectMemoryProxyOptions> options, ILoggerFactory loggerFactory, ILogger<BasicMemoryClient> logger)
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Events

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return tools.ToArray();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_client != null)
            await _client.DisposeAsync().ConfigureAwait(false);

        _connectionLock.Dispose();
    }

    #endregion

    #region Private Methods

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client != null)
            return _client;

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_client != null)
                return _client;

            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                    {
                        Endpoint = _options.BasicMemoryEndpoint,
                        TransportMode = HttpTransportMode.StreamableHttp,
                        ConnectionTimeout = _options.BasicMemoryConnectionTimeout,
                        EnableStandaloneGetStream = false
                    });

            _client = await McpClient.CreateAsync(transport, loggerFactory: _loggerFactory, cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Connected to Basic Memory MCP upstream at {Endpoint}.", _options.BasicMemoryEndpoint);
            return _client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    #endregion
}
