# LUSharp Roblox Studio Plugin Design

**Date:** 2026-02-19
**Status:** Approved

## Overview

A self-contained Roblox Studio plugin that lets users write C# directly in Studio, transpile it to Luau, and run it — with no external tools, servers, or installs. The entire C# parser, transpiler, and IntelliSense engine are implemented in Luau and run natively inside the plugin.

## Architecture

```
[User writes C# in dock widget editor]
         |
   [Build button clicked]
         |
Luau-native C# lexer → tokens
         |
Luau-native recursive descent parser → C# AST
         |
Lowerer → Lua IR
         |
Emitter → Luau source text
         |
Plugin writes Luau into tagged ModuleScript's Source property
Plugin updates diagnostics in the editor
Plugin updates symbol cache for IntelliSense
```

**Single deliverable:** `LUSharp.rbxmx` — a Roblox Studio plugin model. No server, no API, fully offline.

## Script Organization

C# scripts are tagged ModuleScripts with a `CSharpSource` StringValue child:

```
ServerScriptService/
  ServerMain (ModuleScript) [Tagged: "LUSharp"]
    Source: [compiled Luau — auto-generated]
    CSharpSource (StringValue)
      Value: "public class ServerMain : RobloxScript { ... }"

StarterPlayerScripts/
  ClientMain (ModuleScript) [Tagged: "LUSharp"]
    Source: [compiled Luau]
    CSharpSource (StringValue)
      Value: "public class ClientMain : RobloxScript { ... }"

ReplicatedStorage/
  Shared/
    SharedModule (ModuleScript) [Tagged: "LUSharp"]
      Source: [compiled Luau]
      CSharpSource (StringValue)
        Value: "public class SharedModule : ModuleScript { ... }"
```

- Users never edit the Source property directly — the dock widget editor reads/writes the StringValue
- CollectionService tag `"LUSharp"` identifies managed scripts
- On build, compiled Luau is written to Source; C# stays in the StringValue

## Plugin Module Structure

| Module | Responsibility |
|--------|---------------|
| `Init` | Entry point. Creates toolbar, buttons, dock widgets. Wires modules together. |
| `ScriptManager` | Discovers tagged ModuleScripts via CollectionService. Creates/deletes C# scripts. Manages CSharpSource StringValue children. |
| `Editor` | Dock widget code editor. Hidden TextBox + RichText TextLabel overlay. Handles cursor, Tab intercept, auto-indent. |
| `SyntaxHighlighter` | Tokenizes C# source, generates RichText markup with colors. Re-renders on text change (debounced). |
| `Lexer` | C# tokenizer. Produces a stream of tokens (keyword, identifier, string, number, operator, etc.). ~800-1200 lines. |
| `Parser` | Recursive descent C# parser. Consumes tokens, produces C# AST. ~2000-3500 lines. |
| `Lowerer` | Transforms C# AST into Lua IR (tables representing Luau constructs). ~1000-2000 lines. |
| `Emitter` | Walks Lua IR and produces Luau source text with proper indentation. ~400-600 lines. |
| `IntelliSense` | Autocomplete popup, parameter hints, inline diagnostics. Powered by TypeDatabase + symbol cache. |
| `TypeDatabase` | Static Luau table of all LUSharpAPI/Roblox types, members, method signatures, and docs. Auto-generated from LUSharpAPI project. |
| `ProjectView` | Dock widget showing all C# scripts organized by context (Server/Client/Shared). Tree view with create/rename/delete. |
| `Settings` | Configuration panel. Editor theme, font size, tab width, keybindings. Persisted via `plugin:SetSetting()`. |

## Code Editor

### Rendering Architecture

```
ScrollingFrame (clips + scrolls)
+-- Frame (horizontal layout)
    +-- TextLabel (line numbers, right-aligned, gray, monospace)
    +-- Frame (code area)
        +-- TextBox (hidden/transparent, captures input, MultiLine=true)
        +-- TextLabel (RichText=true, displays colored code, non-interactive)
```

TextBox captures input, TextLabel displays the same text with RichText syntax coloring. Synchronized on every TextChanged event.

### Syntax Coloring

| Token | Color (dark theme) |
|-------|-------------------|
| Keyword | `#569CD6` (blue) — class, void, if, public, etc. |
| Type | `#4EC9B0` (teal) — string, int, Vector3, Part, etc. |
| String | `#CE9178` (orange) — "hello", $"interp" |
| Number | `#B5CEA8` (green) — 42, 3.14f |
| Comment | `#6A9955` (gray-green) — // and /* */ |
| Method | `#DCDCAA` (yellow) — GameEntry(), print() |
| Default | `#D4D4D4` (light gray) |

### Input Handling

- Tab: intercepted via UserInputService, inserts 4 spaces
- Enter: newline + auto-indent (matches previous line, adds indent after `{`)
- `}`: auto-dedent
- Ctrl+Z/Y: native TextBox undo/redo

## Luau-Native Transpiler

### Lexer

Tokenizes C# source into a stream of typed tokens. Handles:
- Keywords (class, void, if, else, for, foreach, while, return, new, var, using, namespace, public, private, static, override, virtual, abstract, sealed, const, readonly, etc.)
- Identifiers
- Numeric literals (int, float, double, hex)
- String literals (regular, verbatim @"", interpolated $"")
- Operators and punctuation
- Single-line (//) and multi-line (/* */) comments
- Whitespace and newlines (tracked for position info)

Each token carries: type, value, line, column.

### Parser (Recursive Descent)

Produces a C# AST from the token stream. Supported constructs:

**Declarations:**
- using directives
- namespace declarations
- Class declarations (with base class, interfaces, access modifiers)
- Method declarations (parameters, return type, body)
- Property declarations (get/set, auto-properties, initializers)
- Field declarations (with initializers)
- Constructor declarations
- Interface declarations
- Enum declarations
- Event declarations

**Statements:**
- Variable declarations (var + typed)
- Assignments (=, +=, -=, *=, /=)
- if / else if / else
- for, foreach, while, do-while
- switch (basic, with case/default)
- return, break, continue
- try / catch / finally, throw
- Expression statements (method calls, etc.)

**Expressions:**
- Binary operators (+, -, *, /, %, ==, !=, <, >, <=, >=, &&, ||, &, |)
- Unary operators (!, -, ++, --)
- Member access (dot notation)
- Method invocation (with arguments)
- Object creation (new, with initializers)
- Array/collection initializers
- Lambda expressions (=> and block body)
- String interpolation ($"...")
- Type casting ((Type)expr, as, is)
- Null-conditional (?.), null-coalescing (??)
- Ternary (? :)
- typeof (basic)

**Generics (basic):**
- Generic type usage: List<T>, Dictionary<K,V>
- Generic type arguments on method calls

### Lowerer (C# AST → Lua IR)

Transforms the C# AST into Lua IR tables. Key mappings:

| C# | Lua IR |
|---|---|
| class Foo { } | local Foo = {} / Class.new("Foo") |
| class Foo : Bar | Class.new("Foo", Bar) |
| public int X { get; set; } = 5 | Foo.X = 5 + get_X/set_X methods |
| void Method() { } | function Foo:Method() end |
| static void Method() { } | function Foo.Method() end |
| new Foo(args) | Foo.new(args) |
| List<int> { 1, 2, 3 } | {1, 2, 3} |
| Dictionary<string,int> | {key = val} |
| Console.WriteLine(x) | print(x) |
| $"Hello {name}" | `Hello {name}` |
| event Action<T> OnFoo | Signal.new() |
| OnFoo += handler | OnFoo:Connect(handler) |
| async/await | coroutine / task.spawn |
| foreach (var x in list) | for _, x in pairs(list) do |
| string/int/float/bool | string/number/number/boolean |
| null | nil |

### Emitter (Lua IR → Luau text)

Walks the Lua IR tree and produces indented Luau source. Handles:
- Proper indentation tracking
- Statement separation
- Function formatting
- Table literal formatting
- String escaping

## IntelliSense Engine

### Type Database

Auto-generated Luau table from LUSharpAPI, bundled with the plugin:

```lua
TypeDatabase = {
    Instance = {
        kind = "class",
        base = nil,
        members = {
            Name = { kind = "property", type = "string", doc = "The name of the instance" },
            Parent = { kind = "property", type = "Instance?", doc = "The parent" },
            Destroy = { kind = "method", params = {}, returns = "void" },
            FindFirstChild = { kind = "method", params = {{"name","string"},{"recursive","bool?"}}, returns = "Instance?" },
        },
    },
    Part = { kind = "class", base = "BasePart", members = { ... } },
    Vector3 = {
        kind = "struct",
        constructors = { { params = {{"x","number"},{"y","number"},{"z","number"}} } },
        members = { X = {...}, Y = {...}, Z = {...}, Magnitude = {...}, Unit = {...} },
    },
    __keywords = { "class", "void", "public", "private", ... },
}
```

### Autocomplete Triggers

- `.` after expression: resolve type, show members
- Start of identifier: fuzzy-match keywords, types, locals
- `new `: show constructable types
- `(` after method: show parameter hints

### Diagnostics

Updated on each build. The parser reports errors with position info. Rendered as colored underlines in the RichText overlay:
- Syntax errors (red): missing semicolons, unclosed braces, invalid tokens
- Type errors (yellow): unknown member, wrong argument count
- Warnings (blue): unused variables, unreachable code

## Plugin UI Layout

```
Studio Window
+-- Plugins Toolbar: [New C# Script] [Build] [Build All] [Settings]
+-- Left/Right Dock: Project View
|   +-- Server/
|   |   +-- ServerMain.cs
|   +-- Client/
|   |   +-- ClientMain.cs
|   +-- Shared/
|       +-- SharedModule.cs
+-- Bottom/Right Dock: Code Editor
    +-- Line numbers | Code area (syntax colored)
    +-- Autocomplete popup (floating)
    +-- Status bar: filename | Ln X, Col Y | Build status
```

### Toolbar Actions

- **New C# Script**: creates a tagged ModuleScript with CSharpSource child. Prompts for name and context (Server/Client/Shared).
- **Build**: compiles the currently open C# script. Writes Luau to Source, updates diagnostics.
- **Build All**: compiles every tagged script in order. Updates all Sources and the symbol cache.
- **Settings**: opens settings panel (theme, font, tab width).

### Project View

- Tree of all tagged ModuleScripts, organized by Roblox location
- Click to open in the code editor
- Right-click: rename, delete, duplicate
- Shows build status icons (checkmark, error, warning) per script

## Configuration

Stored via `plugin:SetSetting()`, persists across Studio sessions:

| Setting | Default | Options |
|---------|---------|---------|
| Theme | Dark | Dark, Light |
| Font Size | 14 | 10-24 |
| Tab Width | 4 | 2, 4, 8 |
| Auto-indent | true | true, false |
| Show line numbers | true | true, false |

## Build Flow

1. User clicks Build (or Build All)
2. Plugin reads CSharpSource StringValue from the tagged ModuleScript
3. Lexer tokenizes the C# source
4. Parser produces C# AST (or diagnostics on failure)
5. Lowerer transforms to Lua IR
6. Emitter produces Luau text
7. Plugin writes Luau into ModuleScript.Source via ChangeHistoryService (undoable)
8. Plugin updates diagnostics in the editor
9. Plugin caches symbols (class names, members, types) for IntelliSense cross-file resolution

## Estimated Size

| Component | Lines of Luau |
|-----------|--------------|
| Lexer | 800-1,200 |
| Parser | 2,000-3,500 |
| Lowerer | 1,000-2,000 |
| Emitter | 400-600 |
| IntelliSense | 500-800 |
| Type Database | 1,000-2,000 (auto-generated) |
| Editor UI | 500-800 |
| Project View | 300-500 |
| ScriptManager | 200-300 |
| Settings | 100-200 |
| Init + glue | 100-200 |
| **Total** | **~7,000-12,000** |
