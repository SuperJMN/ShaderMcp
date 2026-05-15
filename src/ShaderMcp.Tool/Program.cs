using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ShaderMcp.Core;
using ShaderMcp.Tool;
using ShaderMcp.Tool.Tools;

if (args.Length > 0)
{
    var exitCode = await ShaderCli.Run(args);
    Environment.Exit(exitCode);
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new ShaderWorkspace(ShaderWorkspaceRoot.ResolveDefault()));
builder.Services.AddSingleton<NativeShaderRunner>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "shader-mcp",
            Version = "0.1.0",
        };
    })
    .WithStdioServerTransport()
    .WithRegisteredTools();

await builder.Build().RunAsync();
