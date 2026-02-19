# Installation

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) — required for C# IntelliSense and project compilation
- [Rojo](https://rojo.space) — for syncing transpiled Luau output into Roblox Studio

## Download LUSharp

Download the latest release binary for your platform from [GitHub Releases](https://github.com/LUSharp/LUSharp/releases).

=== "Windows"

    1. Download `lusharp-win-x64.zip`
    2. Extract to a folder (e.g., `C:\lusharp\`)
    3. Add the folder to your PATH:

        ```powershell
        # PowerShell (run as admin)
        [Environment]::SetEnvironmentVariable(
            "Path",
            [Environment]::GetEnvironmentVariable("Path", "Machine") + ";C:\lusharp",
            "Machine"
        )
        ```

    4. Restart your terminal and verify:

        ```bash
        lusharp help
        ```

=== "Linux"

    1. Download `lusharp-linux-x64.tar.gz`
    2. Extract and move to a location on your PATH:

        ```bash
        tar -xzf lusharp-linux-x64.tar.gz
        sudo mv lusharp /usr/local/bin/
        ```

    3. Verify:

        ```bash
        lusharp help
        ```

=== "macOS"

    1. Download `lusharp-osx-x64.tar.gz`
    2. Extract and move to a location on your PATH:

        ```bash
        tar -xzf lusharp-osx-x64.tar.gz
        sudo mv lusharp /usr/local/bin/
        ```

    3. Verify:

        ```bash
        lusharp help
        ```

## Install Rojo

LUSharp uses [Rojo](https://rojo.space) to sync transpiled Luau into Roblox Studio.

```bash
# Install via Aftman (recommended)
aftman add rojo-rbx/rojo

# Or install via Cargo
cargo install rojo
```

Then install the [Rojo plugin](https://create.roblox.com/store/asset/13916111004) in Roblox Studio.

## Verify Installation

```bash
lusharp help
dotnet --version    # should show 9.x
rojo --version
```

If all three commands succeed, you're ready to [create your first project](quick-start.md).
