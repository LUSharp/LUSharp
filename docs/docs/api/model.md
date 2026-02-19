# Model

```
Instance > PVInstance > Model
```

A container for a group of parts and other objects that form a single entity — characters, vehicles, buildings, or any assembled structure.

[:octicons-link-external-16: Roblox API Reference](https://create.roblox.com/docs/reference/engine/classes/Model){ .md-button .md-button--primary }

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `PrimaryPart` | `BasePart` | The part used as the reference for `MoveTo()` and `GetPivot()` |
| `WorldPivot` | `CFrame` | The world-space pivot of the model |
| `ModelStreamingMode` | `ModelStreamingMode` | Controls how the model streams to clients |

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `MoveTo(Vector3 position)` | `void` | Moves the model so its PrimaryPart is at the given position |
| `GetExtentsSize()` | `Vector3` | Returns the size of the model's bounding box |
| `GetBoundingBox()` | `List<object>` | Returns the CFrame and size of the bounding box |
| `GetScale()` | `double` | Returns the current scale factor |
| `ScaleTo(double newScaleFactor)` | `void` | Scales the model uniformly |
| `TranslateBy(Vector3 delta)` | `void` | Moves the model by a delta offset |
| `GetPersistentPlayers()` | `List<Instance>` | Returns players this model persists for |
| `AddPersistentPlayer(Player player)` | `void` | Adds a persistent player |
| `RemovePersistentPlayer(Player player)` | `void` | Removes a persistent player |

Inherits `GetPivot()` and `PivotTo()` from [PVInstance](base-part.md#inherited-methods-from-pvinstance), plus all [Instance](instance.md) methods and events.

## Examples

=== "C#"

    ```csharp
    var model = Instance.New<Model>(workspace);
    model.Name = "Tower";

    var base = Instance.New<Part>(model);
    base.Size = new Vector3(10, 1, 10);
    base.Anchored = true;
    model.PrimaryPart = base;

    // Move the entire model
    model.MoveTo(new Vector3(0, 10, 0));

    // Scale it up 2x
    model.ScaleTo(2);
    ```

=== "Luau"

    ```lua
    local model = Instance.new("Model")
    model.Parent = workspace
    model.Name = "Tower"

    local base = Instance.new("Part")
    base.Parent = model
    base.Size = Vector3.new(10, 1, 10)
    base.Anchored = true
    model.PrimaryPart = base

    model:MoveTo(Vector3.new(0, 10, 0))
    model:ScaleTo(2)
    ```
