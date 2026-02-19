# Contributing to LUSharp

## Building from Source

```bash
git clone https://github.com/LUSharp/LUSharp.git
cd LUSharp
dotnet build
dotnet test
```

## Project Structure

| Directory | Description |
|-----------|-------------|
| `LUSharp/` | CLI project (`lusharp` binary) |
| `LUSharpTranspiler/` | Core transpiler engine |
| `LUSharpAPI/` | Roblox API C# bindings |
| `LUSharpApiGenerator/` | Generates API stubs from Roblox JSON dump |
| `LUSharpTests/` | Unit tests |

## Running

```bash
# Run the CLI
dotnet run --project LUSharp -- new TestProject
dotnet run --project LUSharp -- build
dotnet run --project LUSharp -- help

# Run the transpiler directly (uses TestInput/)
dotnet run --project LUSharpTranspiler
```

## Tests

```bash
dotnet test
```

All tests are in `LUSharpTests/`. Add tests for any new transpilation rules or CLI features.

## Key Files

| File | Purpose |
|------|---------|
| `LUSharpTranspiler/Frontend/Transpiler.cs` | Pipeline entry point |
| `LUSharpTranspiler/Frontend/CodeWalker.cs` | Roslyn syntax walker |
| `LUSharpTranspiler/Transpiler/Builder/ClassBuilder.cs` | Core C# to Lua conversion |
| `LUSharpTranspiler/Transform/TransformPipeline.cs` | IR transform pass orchestration |
| `LUSharp/Program.cs` | CLI entry point |
| `LUSharp/Project/ProjectScaffolder.cs` | `lusharp new` scaffolding |

## Pull Requests

1. Fork the repo
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Write tests for new functionality
4. Ensure `dotnet build` and `dotnet test` pass
5. Open a PR against `master`

CI will automatically run build and tests on your PR.
