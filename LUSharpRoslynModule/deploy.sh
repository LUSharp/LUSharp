#!/bin/bash
# Transpile all RoslynSource files and deploy to TestPlugin

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUT_DIR="$SCRIPT_DIR/out"
MODULES_DIR="$REPO_ROOT/TestPlugin/src/modules"

echo "=== Transpiling RoslynSource files ==="
dotnet run --project "$SCRIPT_DIR" -- transpile-all || echo "(some files failed — continuing with successfully transpiled files)"

echo ""
echo "=== Copying to TestPlugin/src/modules ==="
mkdir -p "$MODULES_DIR"
cp "$OUT_DIR"/*.lua "$MODULES_DIR/"
echo "Copied $(ls "$OUT_DIR"/*.lua | wc -l) modules"

# Build TestPlugin if rojo is available
if command -v rojo &> /dev/null; then
    echo ""
    echo "=== Building TestPlugin ==="
    rojo build "$REPO_ROOT/TestPlugin/test-plugin.project.json" -o "$REPO_ROOT/TestPlugin/LUSharp-TestPlugin.rbxmx"
    echo "Built LUSharp-TestPlugin.rbxmx"

    # Copy to Roblox plugins folder
    PLUGINS_DIR="$LOCALAPPDATA/Roblox/Plugins"
    if [ -d "$PLUGINS_DIR" ]; then
        cp "$REPO_ROOT/TestPlugin/LUSharp-TestPlugin.rbxmx" "$PLUGINS_DIR/"
        echo "Installed to $PLUGINS_DIR"
    fi
else
    echo "Rojo not found — skipping plugin build"
fi

echo ""
echo "=== Done ==="
