# Type Mappings

LUSharp maps C# types to their Luau equivalents during transpilation. This page documents every type mapping.

## Primitive Types

| C# Type | Luau Type | Notes |
|---------|-----------|-------|
| `string` | `string` | Direct mapping |
| `int` | `number` | Luau has no integer type |
| `float` | `number` | |
| `double` | `number` | |
| `long` | `number` | |
| `bool` | `boolean` | |
| `void` | `nil` | Return type only |
| `object` | `table` | |
| `dynamic` | `any` | |

!!! note
    Luau has a single `number` type that represents all numeric values. C# integer and floating-point types all map to `number`.

## Collection Types

| C# Type | Luau Type | Example |
|---------|-----------|---------|
| `List<T>` | `{T}` (array table) | `{1, 2, 3}` |
| `Dictionary<K, V>` | `{[K]: V}` (table) | `{key = val}` |
| `T[]` | `{T}` (array table) | `{1, 2, 3}` |

### List Example

=== "C#"

    ```csharp
    var numbers = new List<int> { 1, 2, 3, 4, 5 };
    var names = new List<string> { "Alice", "Bob" };
    ```

=== "Luau"

    ```lua
    local numbers = {1, 2, 3, 4, 5}
    local names = {"Alice", "Bob"}
    ```

### Dictionary Example

=== "C#"

    ```csharp
    var map = new Dictionary<string, int>
    {
        { "health", 100 },
        { "speed", 16 },
        { "jump", 50 }
    };
    ```

=== "Luau"

    ```lua
    local map = {health = 100, speed = 16, jump = 50}
    ```

## String Operations

| C# | Luau |
|----|------|
| `$"Hello, {name}!"` | `` `Hello, {name}!` `` |
| `string.Length` | `string.len(s)` |
| `string.ToUpper()` | `string.upper(s)` |
| `string.ToLower()` | `string.lower(s)` |
| `string.Substring(i, len)` | `string.sub(s, i+1, i+len)` |
| `string.Contains(s)` | `string.find(s, pattern)` |
| `Console.WriteLine(x)` | `print(x)` |

!!! warning
    Luau strings are 1-indexed, while C# strings are 0-indexed. LUSharp automatically adjusts string indices during transpilation.

## Roblox Value Types

These types exist natively in Luau and are constructed using their global constructors:

| C# Type | Luau Constructor |
|---------|------------------|
| `Vector3` | `Vector3.new(x, y, z)` |
| `Vector2` | `Vector2.new(x, y)` |
| `CFrame` | `CFrame.new(x, y, z)` |
| `Color3` | `Color3.new(r, g, b)` |
| `BrickColor` | `BrickColor.new("name")` |
| `UDim2` | `UDim2.new(sx, ox, sy, oy)` |
| `UDim` | `UDim.new(s, o)` |
| `Ray` | `Ray.new(origin, direction)` |
| `Rect` | `Rect.new(x1, y1, x2, y2)` |
| `Region3` | `Region3.new(min, max)` |
| `NumberRange` | `NumberRange.new(min, max)` |
| `NumberSequence` | `NumberSequence.new(keypoints)` |
| `ColorSequence` | `ColorSequence.new(keypoints)` |

### Example

=== "C#"

    ```csharp
    var pos = new Vector3(10, 5, 10);
    var look = new CFrame(pos, Vector3.Zero);
    var color = Color3.FromRGB(255, 0, 0);
    var size = new UDim2(0, 100, 0, 50);
    ```

=== "Luau"

    ```lua
    local pos = Vector3.new(10, 5, 10)
    local look = CFrame.new(pos, Vector3.zero)
    local color = Color3.fromRGB(255, 0, 0)
    local size = UDim2.new(0, 100, 0, 50)
    ```

## Roblox Instance Types

Instance types are created with `Instance.new()` in Luau:

| C# | Luau |
|----|------|
| `new Part()` | `Instance.new("Part")` |
| `new Model()` | `Instance.new("Model")` |
| `new Folder()` | `Instance.new("Folder")` |
| `new ScreenGui()` | `Instance.new("ScreenGui")` |
| `new TextLabel()` | `Instance.new("TextLabel")` |

## Event Types

| C# Pattern | Luau Equivalent |
|------------|-----------------|
| `event += handler` | `event:Connect(handler)` |
| `event -= handler` | `connection:Disconnect()` |
| `event.Wait()` | `event:Wait()` |

## Operator Mappings

| C# Operator | Luau Operator |
|-------------|---------------|
| `+`, `-`, `*`, `/` | `+`, `-`, `*`, `/` |
| `%` | `%` |
| `==`, `!=` | `==`, `~=` |
| `&&` | `and` |
| `\|\|` | `or` |
| `!` | `not` |
| `??` | `or` |
| `?.` | `and` chaining |

## Control Flow

| C# | Luau |
|----|------|
| `if / else if / else` | `if / elseif / else / end` |
| `for (int i = 0; ...)` | `for i = 0, ... do ... end` |
| `foreach (var x in list)` | `for _, x in list do ... end` |
| `while (cond)` | `while cond do ... end` |
| `return` | `return` |
| `break` | `break` |
| `continue` | `continue` |

!!! note
    Luau supports `continue` natively, so C# `continue` maps directly.
