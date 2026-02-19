# Services

Services are singleton objects that provide core Roblox functionality. Access them through `game.GetService<T>()` in C#.

=== "C#"

    ```csharp
    var players = game.GetService<Players>();
    var workspace = game.GetService<Workspace>();
    ```

=== "Luau"

    ```lua
    local Players = game:GetService("Players")
    local Workspace = game:GetService("Workspace")
    ```

---

## Players

```
Instance > Players
```

[:octicons-link-external-16: Roblox API Reference](https://create.roblox.com/docs/reference/engine/classes/Players){ .md-button }

The Players service manages all connected players and provides events for player join/leave.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `BubbleChat` | `bool` | Whether bubble chat is enabled |
| `CharacterAutoLoads` | `bool` | Whether characters respawn automatically |
| `ClassicChat` | `bool` | Whether classic chat is enabled |
| `LocalPlayer` | `Player` | *(Client-only)* The local player |
| `MaxPlayers` | `double` | Maximum number of players in the server |
| `PreferredPlayers` | `double` | Preferred number of players for matchmaking |
| `RespawnTime` | `double` | Time in seconds before a character respawns |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetPlayers()` | `List<Player>` | Returns a list of all connected players |
| `BanAsync(config)` | `void` | Bans players by user ID |

### Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `PlayerAdded` | `Player player` | Fires when a player joins the server |
| `PlayerRemoving` | `Player player, PlayerExitReason reason` | Fires when a player is about to leave |

### Examples

#### Welcome Message

=== "C#"

    ```csharp
    var players = game.GetService<Players>();

    players.PlayerAdded += (player) =>
    {
        print($"Welcome, {player.Name}!");
    };

    players.PlayerRemoving += (player, reason) =>
    {
        print($"{player.Name} has left the game");
    };
    ```

=== "Luau"

    ```lua
    local Players = game:GetService("Players")

    Players.PlayerAdded:Connect(function(player)
        print(`Welcome, {player.Name}!`)
    end)

    Players.PlayerRemoving:Connect(function(player, reason)
        print(`{player.Name} has left the game`)
    end)
    ```

#### Iterating Over Players

=== "C#"

    ```csharp
    var players = game.GetService<Players>();

    foreach (var player in players.GetPlayers())
    {
        print($"Player: {player.Name}");
    }
    ```

=== "Luau"

    ```lua
    local Players = game:GetService("Players")

    for _, player in Players:GetPlayers() do
        print(`Player: {player.Name}`)
    end
    ```

#### Character Spawn Handling

=== "C#"

    ```csharp
    var players = game.GetService<Players>();

    players.PlayerAdded += (player) =>
    {
        player.CharacterAdded += (character) =>
        {
            var humanoid = character.FindFirstChildOfClass("Humanoid");
            if (humanoid != null)
            {
                print($"{player.Name} spawned with {humanoid.Health} HP");
            }
        };
    };
    ```

=== "Luau"

    ```lua
    local Players = game:GetService("Players")

    Players.PlayerAdded:Connect(function(player)
        player.CharacterAdded:Connect(function(character)
            local humanoid = character:FindFirstChildOfClass("Humanoid")
            if humanoid then
                print(`{player.Name} spawned with {humanoid.Health} HP`)
            end
        end)
    end)
    ```

---

## Workspace

```
Instance > PVInstance > WorldRoot > Workspace
```

[:octicons-link-external-16: Roblox API Reference](https://create.roblox.com/docs/reference/engine/classes/Workspace){ .md-button }

The Workspace service represents the 3D world. It contains all parts, models, and other 3D objects visible in the game. It is also available as the `workspace` global.

!!! tip
    You can access Workspace directly through the `workspace` global without calling `game.GetService<Workspace>()`.

### Inherited Methods (from WorldRoot)

| Method | Returns | Description |
|--------|---------|-------------|
| `Raycast(Vector3 origin, Vector3 direction, RaycastParams params)` | `RaycastResult?` | Casts a ray and returns hit information |
| `Blockcast(CFrame cframe, Vector3 size, Vector3 direction, RaycastParams params)` | `RaycastResult?` | Casts a block-shaped ray |
| `Spherecast(Vector3 position, double radius, Vector3 direction, RaycastParams params)` | `RaycastResult?` | Casts a sphere-shaped ray |
| `Shapecast(BasePart part, Vector3 direction, RaycastParams params)` | `RaycastResult?` | Casts using a part's shape |
| `GetPartBoundsInBox(CFrame cframe, Vector3 size, OverlapParams params)` | `List<Instance>` | Returns parts overlapping a box region |
| `GetPartBoundsInRadius(Vector3 position, double radius, OverlapParams params)` | `List<Instance>` | Returns parts within a sphere |
| `GetPartsInPart(BasePart part, OverlapParams params)` | `List<Instance>` | Returns parts overlapping a part |
| `ArePartsTouchingOthers(List<Instance> partList, double overlapIgnored)` | `bool` | Whether any parts in the list touch others |
| `BulkMoveTo(List<Instance> partList, List<CFrame> cframeList, BulkMoveMode mode)` | `void` | Moves multiple parts efficiently in one call |
| `StepPhysics(double dt, List<Instance> parts)` | `void` | Steps physics simulation manually |

### Examples

#### Raycasting

=== "C#"

    ```csharp
    var origin = new Vector3(0, 50, 0);
    var direction = new Vector3(0, -100, 0);
    var rParams = new RaycastParams();

    var result = workspace.Raycast(origin, direction, rParams);
    if (result != null)
    {
        print($"Hit: {result.Instance.Name}");
        print($"Position: {result.Position}");
        print($"Normal: {result.Normal}");
    }
    ```

=== "Luau"

    ```lua
    local origin = Vector3.new(0, 50, 0)
    local direction = Vector3.new(0, -100, 0)
    local rParams = RaycastParams.new()

    local result = workspace:Raycast(origin, direction, rParams)
    if result then
        print(`Hit: {result.Instance.Name}`)
        print(`Position: {result.Position}`)
        print(`Normal: {result.Normal}`)
    end
    ```

#### Finding Parts in a Region

=== "C#"

    ```csharp
    var center = new CFrame(0, 10, 0);
    var size = new Vector3(20, 20, 20);
    var overlapParams = new OverlapParams();

    var parts = workspace.GetPartBoundsInBox(center, size, overlapParams);
    foreach (var part in parts)
    {
        print($"Found: {part.Name}");
    }
    ```

=== "Luau"

    ```lua
    local center = CFrame.new(0, 10, 0)
    local size = Vector3.new(20, 20, 20)
    local overlapParams = OverlapParams.new()

    local parts = workspace:GetPartBoundsInBox(center, size, overlapParams)
    for _, part in parts do
        print(`Found: {part.Name}`)
    end
    ```

#### Sphere Query

=== "C#"

    ```csharp
    var center = new Vector3(0, 5, 0);
    double radius = 50;
    var overlapParams = new OverlapParams();

    var nearbyParts = workspace.GetPartBoundsInRadius(center, radius, overlapParams);
    print($"Found {nearbyParts.Count} parts within {radius} studs");
    ```

=== "Luau"

    ```lua
    local center = Vector3.new(0, 5, 0)
    local radius = 50
    local overlapParams = OverlapParams.new()

    local nearbyParts = workspace:GetPartBoundsInRadius(center, radius, overlapParams)
    print(`Found {#nearbyParts} parts within {radius} studs`)
    ```
