# BrickColor

A legacy color type using Roblox's named color palette. For new code, prefer [`Color3`](color3.md) — but BrickColor is still widely used for team colors, spawn locations, and legacy compatibility.

[:octicons-link-external-16: Roblox API Reference](https://create.roblox.com/docs/reference/engine/datatypes/BrickColor){ .md-button .md-button--primary }

## Constructors

| Constructor | Description |
|-------------|-------------|
| `new BrickColor(string name)` | Creates from a named color (e.g. `"Bright red"`) |
| `new BrickColor(double val)` | Creates from a palette number |
| `new BrickColor(double r, double g, double b)` | Creates the closest BrickColor to the given RGB (0–1) |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | The color name (e.g. `"Bright red"`) |
| `Number` | `double` | The palette number |
| `Color` | `Color3` | The equivalent Color3 value |
| `r` | `double` | Red component (0–1) |
| `g` | `double` | Green component (0–1) |
| `b` | `double` | Blue component (0–1) |

## Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `pallete(double value)` | `BrickColor` | Gets a color from the palette by index |
| `random()` | `BrickColor` | Returns a random BrickColor |
| `White()` | `BrickColor` | White |
| `Gray()` | `BrickColor` | Gray |
| `DarkGray()` | `BrickColor` | Dark gray |
| `Black()` | `BrickColor` | Black |
| `Red()` | `BrickColor` | Red |
| `Yellow()` | `BrickColor` | Yellow |
| `Green()` | `BrickColor` | Green |
| `Blue()` | `BrickColor` | Blue |

## Examples

=== "C#"

    ```csharp
    var part = Instance.New<Part>(workspace);
    part.BrickColor = new BrickColor("Bright red");

    // Team colors
    player.TeamColor = new BrickColor("Bright blue");
    ```

=== "Luau"

    ```lua
    local part = Instance.new("Part")
    part.Parent = workspace
    part.BrickColor = BrickColor.new("Bright red")

    player.TeamColor = BrickColor.new("Bright blue")
    ```
