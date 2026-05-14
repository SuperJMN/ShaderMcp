using Microsoft.Extensions.DependencyInjection;

namespace ShaderMcp.Tool.Tools;

internal static class ToolRegistration
{
    public static IReadOnlyList<Type> RegisteredToolTypes { get; } =
    [
        typeof(ShaderTools),
        typeof(InstructionTools),
    ];

    public static IMcpServerBuilder WithRegisteredTools(this IMcpServerBuilder builder)
        => builder
            .WithTools<ShaderTools>()
            .WithTools<InstructionTools>();
}
