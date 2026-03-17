#!/usr/bin/env python3
"""
Generates selfhost_bundle.lua by embedding all 13 transpiler modules
into a single self-contained Luau file that can run under Luau CLI.

Run: python tools/gen_selfhost_bundle.py
Test: luau selfhost_bundle.lua
"""

import os

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LUAU_OUT = os.path.join(REPO, "LUSharpRoslynModule", "RoslynSource", "luau-out")
RUNTIME_PATH = os.path.join(REPO, "LUSharpRoslynModule", "out", "LUSharpRuntime.lua")
OUTPUT = os.path.join(REPO, "selfhost_bundle.lua")

# Modules in dependency order
MODULES = [
    ("LUSharpRuntime",    RUNTIME_PATH),
    ("SyntaxToken",       os.path.join(LUAU_OUT, "SyntaxToken.lua")),
    ("SyntaxKind",        os.path.join(LUAU_OUT, "SyntaxKind.lua")),
    ("SyntaxNode",        os.path.join(LUAU_OUT, "SyntaxNode.lua")),
    ("SyntaxFacts",       os.path.join(LUAU_OUT, "SyntaxFacts.lua")),
    ("SlidingTextWindow", os.path.join(LUAU_OUT, "SlidingTextWindow.lua")),
    ("SimpleTokenizer",   os.path.join(LUAU_OUT, "SimpleTokenizer.lua")),
    ("DeclarationNodes",  os.path.join(LUAU_OUT, "DeclarationNodes.lua")),
    ("ExpressionNodes",   os.path.join(LUAU_OUT, "ExpressionNodes.lua")),
    ("StatementNodes",    os.path.join(LUAU_OUT, "StatementNodes.lua")),
    ("SyntaxWalker",      os.path.join(LUAU_OUT, "SyntaxWalker.lua")),
    ("SimpleParser",      os.path.join(LUAU_OUT, "SimpleParser.lua")),
    ("SimpleEmitter",     os.path.join(LUAU_OUT, "SimpleEmitter.lua")),
    ("SimpleTranspiler",  os.path.join(LUAU_OUT, "SimpleTranspiler.lua")),
]

# SyntaxToken.cs as embedded test input
CSHARP_SOURCE_PATH = os.path.join(REPO, "LUSharpRoslynModule", "RoslynSource", "SyntaxToken.cs")


def lua_long_string_level(s):
    """Find the minimum long-string bracket level that avoids collisions."""
    level = 0
    while True:
        close = "]" + ("=" * level) + "]"
        if close not in s:
            break
        level += 1
    return level


def main():
    # Read the C# test source
    with open(CSHARP_SOURCE_PATH, "r", encoding="utf-8") as f:
        csharp_source = f.read()

    out = []

    # ── Header ──
    out.append("--!nonstrict")
    out.append("-- selfhost_bundle.lua")
    out.append("-- Auto-generated: bundles all 14 transpiler modules for Luau CLI self-hosting test")
    out.append("-- Run with: luau selfhost_bundle.lua")
    out.append("")
    out.append("-- Module require interception system")
    out.append("-- Each module thinks it does require(script.Parent.X)")
    out.append("-- We intercept via a custom local `require` and `script`")
    out.append("")

    # ── Module cache & loaders ──
    out.append("local moduleCache: {[string]: any} = {}")
    out.append("local moduleLoaders: {[string]: () -> any} = {}")
    out.append("")

    # ── scriptParent proxy ──
    out.append("-- Proxy: script.Parent.X returns a token that require() resolves")
    out.append("local scriptParent = setmetatable({}, {")
    out.append("    __index = function(_, key: string)")
    out.append("        return { _moduleName = key }")
    out.append("    end")
    out.append("})")
    out.append("")

    # ── Custom require ──
    out.append("-- Custom require: resolves module tokens and caches results")
    out.append("local function customRequire(target: any): any")
    out.append('    if type(target) == "table" and target._moduleName then')
    out.append("        local name = target._moduleName")
    out.append("        if moduleCache[name] then")
    out.append("            return moduleCache[name]")
    out.append("        end")
    out.append("        local loader = moduleLoaders[name]")
    out.append("        if not loader then")
    out.append('            error("No loader for module: " .. name)')
    out.append("        end")
    out.append("        local result = loader()")
    out.append("        moduleCache[name] = result")
    out.append("        return result")
    out.append("    end")
    out.append('    error("Cannot require non-bundled module: " .. tostring(target))')
    out.append("end")
    out.append("")

    # ── Shadow global require ──
    out.append("-- Shadow require at module scope")
    out.append("local require = customRequire  -- luacheck: ignore")
    out.append("")

    # ── Embed each module ──
    for mod_name, mod_path in MODULES:
        with open(mod_path, "r", encoding="utf-8") as f:
            source = f.read()

        # Strip --!strict (the bundle uses --!nonstrict)
        # Convert `export type` -> `type` (export is invalid inside function bodies)
        source_lines = source.split("\n")
        filtered = []
        for sl in source_lines:
            stripped = sl.strip()
            if stripped == "--!strict":
                continue
            # export type -> type (must be inside a function now)
            if sl.startswith("export type "):
                sl = sl[len("export "):]
            elif sl.startswith("\texport type "):
                sl = sl[:1] + sl[1 + len("export "):]
            # rawset(_G, ...) -> pcall(rawset, _G, ...) (Luau CLI has readonly _G)
            if "rawset(_G," in sl:
                sl = sl.replace("rawset(_G,", "pcall(rawset, _G,")
            filtered.append(sl)
        source = "\n".join(filtered)

        out.append("-- " + "=" * 60)
        out.append("-- Module: {} ({})".format(mod_name, os.path.basename(mod_path)))
        out.append("-- " + "=" * 60)
        out.append('moduleLoaders["{}"] = function()'.format(mod_name))
        out.append("    local script = { Parent = scriptParent }")

        # Embed module source, indented by 4 spaces.
        # The module's final `return { X = X }` naturally becomes
        # the return value of this loader function.
        for sl in source.split("\n"):
            if sl:
                out.append("    " + sl)
            else:
                out.append("")
        out.append("end")
        out.append("")

    # ── Test harness ──
    out.append("-- " + "=" * 60)
    out.append("-- Self-hosting test: transpile SyntaxToken.cs")
    out.append("-- " + "=" * 60)
    out.append("")
    out.append('print("=== LUSharp Self-Hosting Bundle Test ===")')
    out.append('print("")')
    out.append("")

    # Load all modules
    out.append("-- Load all modules (in dependency order)")
    for mod_name, _ in MODULES:
        out.append('print("Loading {}...")'.format(mod_name))
        out.append('local _{}_mod = customRequire({{ _moduleName = "{}" }})'.format(
            mod_name, mod_name
        ))
    out.append("")
    out.append('print("")')
    out.append('print("All 14 modules loaded successfully!")')
    out.append('print("")')
    out.append("")

    # Embed C# source
    level = lua_long_string_level(csharp_source)
    eq = "=" * level
    out.append("-- Embedded C# test source: SyntaxToken.cs")
    out.append("local testSource = [{}[".format(eq))
    out.append(csharp_source.rstrip())
    out.append("]{}]".format(eq))
    out.append("")

    # Run transpiler
    out.append("-- Create transpiler and transpile the test source")
    out.append('local transpilerMod = customRequire({ _moduleName = "SimpleTranspiler" })')
    out.append("local SimpleTranspiler = transpilerMod.SimpleTranspiler")
    out.append("")
    out.append('print("Creating SimpleTranspiler instance...")')
    out.append("local t = SimpleTranspiler.new()")
    out.append("")
    out.append("-- PreScan (single file)")
    out.append('print("PreScanning SyntaxToken.cs...")')
    out.append('SimpleTranspiler.PreScan(t, { testSource }, { "SyntaxToken.cs" }, 1)')
    out.append("")
    out.append("-- Transpile")
    out.append('print("Transpiling SyntaxToken.cs...")')
    out.append('local result = SimpleTranspiler.Transpile(t, testSource, "SyntaxToken.cs")')
    out.append("")
    out.append('print("")')
    out.append('print("=== Transpiled Luau Output ===")')
    out.append('print("")')
    out.append("print(result)")
    out.append('print("")')
    out.append('print("=== Self-hosting test complete! ===")')
    out.append("")

    # Write
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))

    total = len(out)
    print("Generated {}".format(OUTPUT))
    print("Total lines: {}".format(total))
    print("Modules embedded: {}".format(len(MODULES)))


if __name__ == "__main__":
    main()
