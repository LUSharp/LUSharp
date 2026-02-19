# Contributing

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

## Development Workflow

1. Fork the repo
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Write tests for new functionality
4. Ensure `dotnet build` and `dotnet test` pass
5. Open a PR against `master`

CI will automatically run build and tests on your PR.

## Adding a New Type Mapping

To add support for a new C# → Luau conversion:

1. Add the C# type stub to `LUSharpAPI/` (if it's a Roblox type)
2. Add the type mapping to `TypeResolver` in the transform layer
3. Add transpilation logic to `ClassBuilder` or the relevant builder
4. Write unit tests in `LUSharpTests/`
5. Document the mapping in the [Type Mappings](guide/type-mappings.md) guide

## Adding a New Transform Pass

1. Create a new pass class implementing the transform pass interface
2. Register it in `TransformPipeline.cs` in the correct order
3. Write unit tests covering the transformation
4. Document the pass in the [Architecture](architecture.md) page

## Code Style

- Follow standard C# conventions
- Use `var` for local variables when the type is obvious
- XML documentation on public APIs
- Keep classes focused — one responsibility per class
