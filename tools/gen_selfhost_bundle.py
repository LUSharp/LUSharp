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

# All 13 C# source files as test input
CSHARP_FILES = [
    "SyntaxToken.cs", "SyntaxNode.cs", "SyntaxKind.cs", "SyntaxFacts.cs",
    "SlidingTextWindow.cs", "SimpleTokenizer.cs", "SimpleParser.cs",
    "DeclarationNodes.cs", "ExpressionNodes.cs", "StatementNodes.cs",
    "SyntaxWalker.cs", "SimpleEmitter.cs", "SimpleTranspiler.cs",
]
CSHARP_SOURCE_DIR = os.path.join(REPO, "LUSharpRoslynModule", "RoslynSource")
REFERENCE_DIR = os.path.join(REPO, "LUSharpRoslynModule", "RoslynSource", "luau-out")


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
    out.append("-- Self-hosting test: transpile all 13 files and compare")
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

    # Embed all 13 C# source files
    out.append("-- Embedded C# source files")
    out.append("local csharpSources = {}")
    out.append("local csharpNames = {}")
    out.append("local fileCount = {}".format(len(CSHARP_FILES)))
    for i, cs_file in enumerate(CSHARP_FILES):
        cs_path = os.path.join(CSHARP_SOURCE_DIR, cs_file)
        with open(cs_path, "r", encoding="utf-8") as f:
            cs_src = f.read()
        level = lua_long_string_level(cs_src)
        eq = "=" * level
        out.append('csharpNames[{}] = "{}"'.format(i + 1, cs_file))
        out.append("csharpSources[{}] = [{}[".format(i + 1, eq))
        out.append(cs_src.rstrip())
        out.append("]{}]".format(eq))
        out.append("")

    # Embed all 13 reference Luau outputs
    out.append("-- Reference Luau outputs (from C# transpiler)")
    out.append("local references = {}")
    for i, cs_file in enumerate(CSHARP_FILES):
        lua_file = cs_file.replace(".cs", ".lua")
        ref_path = os.path.join(REFERENCE_DIR, lua_file)
        with open(ref_path, "r", encoding="utf-8") as f:
            ref_src = f.read()
        level = lua_long_string_level(ref_src)
        eq = "=" * level
        out.append("references[{}] = [{}[".format(i + 1, eq))
        out.append(ref_src.rstrip())
        out.append("]{}]".format(eq))
        out.append("")

    # Run transpiler on all files
    out.append('local transpilerMod = customRequire({ _moduleName = "SimpleTranspiler" })')
    out.append("local SimpleTranspiler = transpilerMod.SimpleTranspiler")
    out.append("")
    out.append('print("Creating SimpleTranspiler instance...")')
    out.append("local t = SimpleTranspiler.new()")
    out.append("")
    out.append('print("PreScanning " .. fileCount .. " files...")')
    out.append("SimpleTranspiler.PreScan(t, csharpSources, csharpNames, fileCount)")
    out.append('print("TranspileAll...")')
    out.append("local results = SimpleTranspiler.TranspileAll(t, csharpSources, csharpNames, fileCount)")
    out.append('print("")')
    out.append("")

    # Compare results
    out.append("-- Compare each output with reference")
    out.append("local pass, fail = 0, 0")
    out.append("for i = 1, fileCount do")
    out.append('    local name = csharpNames[i]')
    out.append('    local luauOut = results[i]')
    out.append('    local ref = references[i]')
    out.append('    if luauOut == nil then')
    out.append('        fail = fail + 1')
    out.append('        print("  FAIL  " .. name .. " — nil output")')
    out.append('    else')
    out.append('        -- Trim trailing whitespace/newlines for comparison')
    out.append('        local function trim(s) while #s > 0 and (string.byte(s, #s) == 10 or string.byte(s, #s) == 13 or string.byte(s, #s) == 32) do s = string.sub(s, 1, #s - 1) end return s end')
    out.append('        local a = trim(luauOut)')
    out.append('        local b = trim(ref)')
    out.append('        if a == b then')
    out.append('            pass = pass + 1')
    out.append('            print("  MATCH " .. name)')
    out.append('        else')
    out.append('            fail = fail + 1')
    out.append('            -- Find first differing line')
    out.append('            local aLines, bLines = {}, {}')
    out.append('            for line in (a .. "\\n"):gmatch("([^\\n]*)\\n") do table.insert(aLines, line) end')
    out.append('            for line in (b .. "\\n"):gmatch("([^\\n]*)\\n") do table.insert(bLines, line) end')
    out.append('            local firstDiff = "?"')
    out.append('            for j = 1, math.max(#aLines, #bLines) do')
    out.append('                if aLines[j] ~= bLines[j] then')
    out.append('                    firstDiff = "line " .. j .. ":\\n      Luau:  " .. tostring(aLines[j]):sub(1,80) .. "\\n      C#ref: " .. tostring(bLines[j]):sub(1,80)')
    out.append('                    break')
    out.append('                end')
    out.append('            end')
    out.append('            print("  DIFF  " .. name .. " (" .. #aLines .. " vs " .. #bLines .. " lines) first: " .. firstDiff)')
    out.append('        end')
    out.append('    end')
    out.append("end")
    out.append("")
    out.append('print("")')
    out.append('print(pass .. "/" .. (pass + fail) .. " files match")')
    out.append('if pass == fileCount then print("PERFECT MATCH — Luau transpiler produces identical output to C# transpiler!") end')
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
