#!/usr/bin/env python3
"""
Generates test_bundle.lua by transpiling all test files and bundling them
into a single self-contained Luau file that can run under Luau CLI.

Run: python tools/gen_test_bundle.py
Test: luau test_bundle.lua
"""

import os
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEST_DIR = os.path.join(REPO, "LUSharpRoslynModule", "Tests")
RUNTIME_PATH = os.path.join(REPO, "LUSharpRoslynModule", "out", "LUSharpRuntime.lua")
OUTPUT = os.path.join(REPO, "test_bundle.lua")
PROJECT = os.path.join(REPO, "LUSharpRoslynModule", "LUSharpRoslynModule.csproj")


def lua_long_string_level(s):
    level = 0
    while True:
        close = "]" + ("=" * level) + "]"
        if close not in s:
            break
        level += 1
    return level


def main():
    # Step 1: Transpile all test files using the C# transpiler
    print("Transpiling test files...")
    result = subprocess.run(
        ["dotnet", "run", "--project", PROJECT, "--", "transpile-project", TEST_DIR],
        capture_output=True, text=True, cwd=REPO
    )
    print(result.stdout)
    if result.returncode != 0:
        print("Transpilation failed:")
        print(result.stderr)
        sys.exit(1)

    luau_out = os.path.join(TEST_DIR, "luau-out")
    if not os.path.exists(luau_out):
        print("No luau-out directory created")
        sys.exit(1)

    # Collect all transpiled .lua files
    lua_files = sorted([f for f in os.listdir(luau_out) if f.endswith(".lua") and f != "LUSharpRuntime.lua"])
    print(f"Found {len(lua_files)} transpiled test modules")

    # Step 2: Build the bundle
    out = []
    out.append("--!nonstrict")
    out.append("-- test_bundle.lua")
    out.append("-- Auto-generated: bundles all transpiled test modules for Luau CLI execution")
    out.append("-- Run: luau test_bundle.lua")
    out.append("")

    # Luau CLI compatibility
    out.append("local warn = warn or print")
    out.append("")

    # Module loader infrastructure
    out.append("local moduleLoaders = {}")
    out.append("local moduleCache = {}")
    out.append("")
    out.append("local scriptParent = setmetatable({}, {")
    out.append('    __index = function(_, name) return { _moduleName = name } end')
    out.append("})")
    out.append("")
    out.append("local function customRequire(target)")
    out.append('    if type(target) == "table" and target._moduleName then')
    out.append("        local name = target._moduleName")
    out.append("        if moduleCache[name] then return moduleCache[name] end")
    out.append("        local loader = moduleLoaders[name]")
    out.append('        if not loader then error("Module not found: " .. name) end')
    out.append("        local result = loader()")
    out.append("        moduleCache[name] = result")
    out.append("        return result")
    out.append("    end")
    out.append('    error("Cannot require: " .. tostring(target))')
    out.append("end")
    out.append("")

    # Embed the runtime
    print("Embedding LUSharpRuntime...")
    with open(RUNTIME_PATH, "r", encoding="utf-8") as f:
        runtime_source = f.read()

    source_lines = runtime_source.split("\n")
    filtered = []
    for sl in source_lines:
        stripped = sl.strip()
        if stripped == "--!strict":
            continue
        if sl.startswith("export type "):
            sl = sl[len("export "):]
        elif sl.startswith("\texport type "):
            sl = sl[:1] + sl[1 + len("export "):]
        # rawset(_G, ...) -> pcall(rawset, _G, ...) for Luau CLI
        if "rawset(_G," in sl:
            sl = sl.replace("rawset(_G,", "pcall(rawset, _G,")
        filtered.append(sl)
    runtime_source = "\n".join(filtered)

    out.append("-- " + "=" * 60)
    out.append("-- Module: LUSharpRuntime")
    out.append("-- " + "=" * 60)
    out.append('moduleLoaders["LUSharpRuntime"] = function()')
    out.append("    local script = { Parent = scriptParent }")
    out.append("    local require = customRequire")
    for sl in runtime_source.split("\n"):
        if sl:
            out.append("    " + sl)
        else:
            out.append("")
    out.append("end")
    out.append("")

    # Embed each test module
    for lua_file in lua_files:
        mod_name = lua_file[:-4]  # strip .lua
        mod_path = os.path.join(luau_out, lua_file)
        print(f"Embedding {mod_name}...")

        with open(mod_path, "r", encoding="utf-8") as f:
            source = f.read()

        # Transform for bundle context
        source_lines = source.split("\n")
        filtered = []
        for sl in source_lines:
            stripped = sl.strip()
            if stripped == "--!strict":
                continue
            if sl.startswith("export type "):
                sl = sl[len("export "):]
            elif sl.startswith("\texport type "):
                sl = sl[:1] + sl[1 + len("export "):]
            if "rawset(_G," in sl:
                sl = sl.replace("rawset(_G,", "pcall(rawset, _G,")
            filtered.append(sl)
        source = "\n".join(filtered)

        out.append("-- " + "=" * 60)
        out.append(f"-- Module: {mod_name}")
        out.append("-- " + "=" * 60)
        out.append(f'moduleLoaders["{mod_name}"] = function()')
        out.append("    local script = { Parent = scriptParent }")
        out.append("    local require = customRequire")
        for sl in source.split("\n"):
            if sl:
                out.append("    " + sl)
            else:
                out.append("")
        out.append("end")
        out.append("")

    # Test harness: load and run TestRunner
    out.append("-- " + "=" * 60)
    out.append("-- Run all tests")
    out.append("-- " + "=" * 60)
    out.append("")

    # Load all modules in order
    for lua_file in lua_files:
        mod_name = lua_file[:-4]
        out.append(f'local ok_{mod_name}, err_{mod_name} = pcall(function()')
        out.append(f'    moduleCache["{mod_name}"] = moduleLoaders["{mod_name}"]()')
        out.append(f'end)')
        out.append(f'if not ok_{mod_name} then')
        out.append(f'    warn("Failed to load {mod_name}: " .. tostring(err_{mod_name}))')
        out.append(f'end')
        out.append("")

    # Run each test module with pcall isolation
    out.append('print("=== LUSharp Transpiler Test Suite ===")')
    out.append('print("")')
    test_modules = [f[:-4] for f in sorted(lua_files) if f.startswith("T") and f != "TestRunner.lua"]
    for mod in test_modules:
        out.append(f'if moduleCache["{mod}"] then')
        out.append(f'    for k, v in pairs(moduleCache["{mod}"]) do')
        out.append(f'        if type(v) == "table" and type(v.Run) == "function" then')
        out.append(f'            local ok, err = pcall(v.Run)')
        out.append(f'            if not ok then')
        out.append(f'                warn("  CRASH in " .. k .. ".Run: " .. tostring(err))')
        out.append(f'            end')
        out.append(f'        end')
        out.append(f'    end')
        out.append(f'end')
    out.append('print("")')
    out.append('print("=== All test categories complete ===")')

    # Write output
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))

    total = len(out)
    print(f"\nGenerated {OUTPUT}")
    print(f"Total lines: {total}")
    print(f"Test modules embedded: {len(lua_files)}")
    print(f"\nRun: luau test_bundle.lua")


if __name__ == "__main__":
    main()
