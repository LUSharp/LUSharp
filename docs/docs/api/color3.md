# Color3

A color value with red, green, and blue components (0–1 range). Used for part colors, GUI colors, lighting, and more.

[:octicons-link-external-16: Roblox API Reference](https://create.roblox.com/docs/reference/engine/datatypes/Color3){ .md-button .md-button--primary }

## Constructors

| Constructor | Description |
|-------------|-------------|
| `new Color3(double r, double g, double b)` | Creates a Color3 from R, G, B values (0–1) |

## Factory Methods

| Method | Description |
|--------|-------------|
| `Color3.fromRGB(double r, double g, double b)` | Creates a Color3 from RGB values (0–255) |
| `Color3.fromHSV(double h, double s, double v)` | Creates a Color3 from hue, saturation, value (0–1) |
| `Color3.fromHex(string hex)` | Creates a Color3 from a hex string (e.g. `"#FF0000"`) |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `R` | `double` | Red component (0–1) |
| `G` | `double` | Green component (0–1) |
| `B` | `double` | Blue component (0–1) |

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Lerp(Color3 color, double alpha)` | `Color3` | Linear interpolation to another color |
| `ToHSV()` | `(double, double, double)` | Converts to hue, saturation, value |
| `ToHex()` | `string` | Converts to hex string |

## Examples

=== "C#"

    ```csharp
    // From 0-255 RGB (most common)
    var red = Color3.fromRGB(255, 0, 0);
    var skyBlue = Color3.fromRGB(135, 206, 235);

    // From 0-1 range
    var green = new Color3(0, 1, 0);

    // From hex
    var purple = Color3.fromHex("#8B00FF");

    // Apply to a part
    var part = Instance.New<Part>(workspace);
    part.Color = red;
    ```

=== "Luau"

    ```lua
    local red = Color3.fromRGB(255, 0, 0)
    local skyBlue = Color3.fromRGB(135, 206, 235)

    local green = Color3.new(0, 1, 0)

    local purple = Color3.fromHex("#8B00FF")

    local part = Instance.new("Part")
    part.Parent = workspace
    part.Color = red
    ```
