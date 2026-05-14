# ShaderMcp

Native MCP server and CLI for creating and testing deterministic Skia RuntimeEffect fragment shaders.

The runner does not use a browser, WebGL, Playwright, Puppeteer, HTML canvas, or a DOM rendering path. It compiles SkSL with SkiaSharp, renders offscreen to PNG, passes deterministic uniforms, supplies a known input texture, and returns structured diagnostics.

## Shader Contract

Shader source defines `fragmentMain`:

```sksl
half4 fragmentMain(float2 fragCoord, float2 uv) {
    return half4(uv.x, uv.y, 0.5, 1.0);
}
```

Built-ins injected by the runner:

- `uniform float2 resolution`
- `uniform float time`
- `uniform float frame`
- `uniform float2 pointer`
- `uniform shader inputTexture`
- manifest parameters as `uniform float4 <name>`

## CLI

Install from NuGet:

```bash
dotnet tool install --global ShaderMcp
shader-mcp --help
```

Run once without installing globally:

```bash
dotnet dnx ShaderMcp --yes -- --help
```

Local development:

```bash
dotnet run --project src/ShaderMcp.Tool -- validate --source samples/gradient.sksl
dotnet run --project src/ShaderMcp.Tool -- render --source samples/gradient.sksl --out artifacts/gradient.png --width 512 --height 512 --time 0.25
dotnet run --project src/ShaderMcp.Tool -- sequence --source samples/gradient.sksl --out-dir artifacts/gradient-sequence --frames 12 --time-step 0.083333
```

## MCP

Run the tool with no CLI args for stdio MCP mode:

```toml
[mcp_servers.shader-mcp]
command = "dotnet"
args = ["dnx", "ShaderMcp", "--yes"]
```

For local development:

```toml
[mcp_servers.shader-mcp]
command = "dotnet"
args = ["run", "--project", "/home/jmn/Repos/ShaderMcp/src/ShaderMcp.Tool"]
```

Main tools:

- `create_shader`
- `update_shader`
- `validate_shader`
- `render_shader`
- `render_shader_sequence`
- `run_shader_tests`
- `instructions(page="tools")`

## License

Licensed under CC BY-NC 4.0. Attribution is required and commercial use is not permitted without separate permission.
