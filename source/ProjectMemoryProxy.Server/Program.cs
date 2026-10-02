using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Reflection;
using ProjectMemoryProxy.Server.Configuration;

// Init logging
var logLevelSwitch = new LoggingLevelSwitch { MinimumLevel = LogEventLevel.Information };
Log.Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(logLevelSwitch)
    .WriteTo.Console(outputTemplate: "[{Timestamp:dd.MM.yyyy HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}", theme: AnsiConsoleTheme.Sixteen, applyThemeToRedirectedOutput: true)
    .CreateLogger();
Log.Information($"Starting ProjectMemoryProxy Server v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}");

var builder = WebApplication.CreateBuilder(args);

// Register logging with Serilog. This will replace the default logging provider.
builder.Services.AddSerilog();
builder.Services.AddSingleton(logLevelSwitch);

// Set log level from configuration, defaulting to Information if not configured or invalid.
var configuredLogLevel = builder.Configuration[$"{ProjectMemoryProxyOptions.SectionName}:LogLevel"] ?? "Information";
if (Enum.TryParse<LogEventLevel>(configuredLogLevel, true, out var minimumLogLevel))
    logLevelSwitch.MinimumLevel = minimumLogLevel;
else
    Log.Error($"Invalid ProjectMemoryProxy log level '{configuredLogLevel}', allowed values are '{string.Join(", ", Enum.GetValues<LogEventLevel>())}', fallback to default level '{logLevelSwitch.MinimumLevel}'!");

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Stateless mode is recommended for servers that don't need
        // server-to-client requests like sampling or elicitation.
        // See https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html for details.
        options.Stateless = true;
    })
    .WithTools<RandomNumberTools>();

var app = builder.Build();
app.MapMcp();
app.UseHttpsRedirection();

app.Run();
