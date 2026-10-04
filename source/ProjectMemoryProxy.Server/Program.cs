using System;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ProjectMemoryProxy.BasicMemory;
using ProjectMemoryProxy.Core.Configuration;
using ProjectMemoryProxy.Persistence;
using ProjectMemoryProxy.Server.Hosting;
using ProjectMemoryProxy.Server.Tools;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

// Force english exception messages
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

// Init logging
var logLevelSwitch = new LoggingLevelSwitch { MinimumLevel = LogEventLevel.Information };
Log.Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(logLevelSwitch)
    .WriteTo.Console(outputTemplate: "[{Timestamp:dd.MM.yyyy HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}", theme: AnsiConsoleTheme.Sixteen, applyThemeToRedirectedOutput: true)
    .CreateLogger();
Log.Information($"Starting Server v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}");

var builder = WebApplication.CreateBuilder(args);

// Register logging with Serilog. This will replace the default logging provider.
builder.Services.AddSerilog();
builder.Services.AddSingleton(logLevelSwitch);

// Set log level from configuration, defaulting to Information if not configured or invalid.
var configuredLogLevel = builder.Configuration[$"{ProjectMemoryProxyOptions.SectionName}:{nameof(ProjectMemoryProxyOptions.LogLevel)}"] ?? "Information";
if (Enum.TryParse<LogEventLevel>(configuredLogLevel, true, out var minimumLogLevel))
    logLevelSwitch.MinimumLevel = minimumLogLevel;
else
    Log.Error($"Invalid log level '{configuredLogLevel}', allowed values are '{string.Join(", ", Enum.GetValues<LogEventLevel>())}', fallback to default level '{logLevelSwitch.MinimumLevel}'!");

// Register the configuration section and validate it on startup.
builder.Services.AddOptions<ProjectMemoryProxyOptions>()
    .Bind(builder.Configuration.GetSection(ProjectMemoryProxyOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.DataDirectory), $"{ProjectMemoryProxyOptions.SectionName}__{nameof(ProjectMemoryProxyOptions.DataDirectory)} must be configured.")
    .Validate(options => options.BasicMemoryEndpoint is { IsAbsoluteUri: true }, $"{ProjectMemoryProxyOptions.SectionName}__{nameof(ProjectMemoryProxyOptions.BasicMemoryEndpoint)} must be an absolute URI.")
    .Validate(options => options.BasicMemoryConnectionTimeout > TimeSpan.Zero, $"{ProjectMemoryProxyOptions.SectionName}__{nameof(ProjectMemoryProxyOptions.BasicMemoryConnectionTimeout)} must be greater than zero.")
    //.Validate(options => Enum.TryParse<LogEventLevel>(options.LogLevel, true, out _), "LogLevel must be a valid Serilog log event level.")
    .ValidateOnStart();

// Register the lifecycle database. Schema migrations are applied to an isolated copy during startup before MCP requests are accepted.
builder.Services.AddPersistence();

// Register the Basic Memory upstream integration and block host startup until its toolset is available.
builder.Services.AddBasicMemory();
builder.Services.AddHostedService<BasicMemoryStartupHostedService>();

// Add the MCP services: the transport to use (http) and the tools to register.
var mcpServerBuilder = builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Stateless mode is recommended for servers that don't need
        // server-to-client requests like sampling or elicitation.
        // See https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html for details.
        options.SessionMode = HttpServerSessionMode.Stateless;
    });

mcpServerBuilder.WithTools<RoutingTools>();

var app = builder.Build();

// Ensure the lifecycle database is fully initialized or migrated before the MCP endpoint starts.
await app.Services.MigrateDatabaseAsync();

app.MapMcp();
app.Run();
