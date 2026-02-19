# Raycasting

Raycasting is used to detect objects in the 3D world by projecting rays, blocks, or spheres. LUSharp provides C# bindings for Roblox's spatial query API.

[:octicons-link-external-16: Roblox Raycasting Guide](https://create.roblox.com/docs/workspace/raycasting){ .md-button .md-button--primary }

## RaycastParams

Configuration for raycast and spatial queries.

| Property | Type | Description |
|----------|------|-------------|
| `FilterDescendantsInstances` | `List<RObject>` | Instances to include/exclude from the query |
| `FilterType` | `RaycastFilterType` | `Include` or `Exclude` — whether the filter list is an allowlist or blocklist |
| `IgnoreWater` | `bool` | Whether to ignore Terrain water |
| `CollisionGroup` | `string` | Collision group to use for filtering |
| `RespectCanCollide` | `bool` | Whether to respect the `CanCollide` property |

### Methods

| Method | Description |
|--------|-------------|
| `AddToFilter(params RObject[] instances)` | Adds instances to the filter list |

## RaycastResult

Returned by raycast methods when a hit is detected. `null` if nothing was hit.

| Property | Type | Description |
|----------|------|-------------|
| `Instance` | `BasePart` | The part that was hit |
| `Position` | `Vector3` | World position of the hit |
| `Normal` | `Vector3` | Surface normal at the hit point |
| `Distance` | `double` | Distance from the ray origin to the hit |
| `Material` | `Material` | The material at the hit point |

## Ray

A geometric ray with an origin and direction.

| Constructor | Description |
|-------------|-------------|
| `new Ray(Vector3 origin, Vector3 direction)` | Creates a ray |

| Property | Type | Description |
|----------|------|-------------|
| `Origin` | `Vector3` | The starting point |
| `Direction` | `Vector3` | The direction vector |
| `Unit` | `Ray` | Normalized version (direction length = 1) |

| Method | Returns | Description |
|--------|---------|-------------|
| `ClosestPoint(Vector3 point)` | `Vector3` | Closest point on the ray to a given point |
| `Distance(Vector3 point)` | `double` | Distance from the ray to a point |

## Workspace Raycast Methods

These methods are on [`Workspace`](services.md#workspace) (or the `workspace` global):

| Method | Description |
|--------|-------------|
| `Raycast(origin, direction, params)` | Casts a ray |
| `Blockcast(cframe, size, direction, params)` | Casts a box shape |
| `Spherecast(position, radius, direction, params)` | Casts a sphere shape |
| `Shapecast(part, direction, params)` | Casts using a part's shape |

## Examples

### Basic Raycast

=== "C#"

    ```csharp
    var origin = new Vector3(0, 50, 0);
    var direction = new Vector3(0, -100, 0);

    var rParams = new RaycastParams();
    rParams.FilterType = RaycastFilterType.Exclude;

    var result = workspace.Raycast(origin, direction, rParams);
    if (result != null)
    {
        print($"Hit {result.Instance.Name} at {result.Position}");
        print($"Surface normal: {result.Normal}");
        print($"Distance: {result.Distance}");
    }
    else
    {
        print("Nothing hit");
    }
    ```

=== "Luau"

    ```lua
    local origin = Vector3.new(0, 50, 0)
    local direction = Vector3.new(0, -100, 0)

    local rParams = RaycastParams.new()
    rParams.FilterType = Enum.RaycastFilterType.Exclude

    local result = workspace:Raycast(origin, direction, rParams)
    if result then
        print(`Hit {result.Instance.Name} at {result.Position}`)
        print(`Surface normal: {result.Normal}`)
        print(`Distance: {result.Distance}`)
    else
        print("Nothing hit")
    end
    ```

### Filtering

=== "C#"

    ```csharp
    var rParams = new RaycastParams();
    rParams.FilterType = RaycastFilterType.Exclude;
    rParams.AddToFilter(player.Character);  // Ignore the player's character
    rParams.IgnoreWater = true;

    var result = workspace.Raycast(origin, direction, rParams);
    ```

=== "Luau"

    ```lua
    local rParams = RaycastParams.new()
    rParams.FilterType = Enum.RaycastFilterType.Exclude
    rParams:AddToFilter(player.Character)
    rParams.IgnoreWater = true

    local result = workspace:Raycast(origin, direction, rParams)
    ```

### Mouse Raycast (Client)

=== "C#"

    ```csharp
    // Cast a ray from the camera through the mouse position
    var mouse = players.LocalPlayer.GetMouse();
    var camera = workspace.FindFirstChildOfClass("Camera");

    var ray = camera.ScreenPointToRay(mouse.X, mouse.Y);
    var result = workspace.Raycast(ray.Origin, ray.Direction * 1000);

    if (result != null)
    {
        print($"Looking at: {result.Instance.Name}");
    }
    ```

=== "Luau"

    ```lua
    local mouse = Players.LocalPlayer:GetMouse()
    local camera = workspace.CurrentCamera

    local ray = camera:ScreenPointToRay(mouse.X, mouse.Y)
    local result = workspace:Raycast(ray.Origin, ray.Direction * 1000)

    if result then
        print(`Looking at: {result.Instance.Name}`)
    end
    ```
