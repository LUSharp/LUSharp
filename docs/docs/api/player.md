# Player

```
Instance > Player
```

Represents a connected player in the game. Access players through the [`Players`](services.md#players) service.

[:octicons-link-external-16: Roblox API Reference](https://create.roblox.com/docs/reference/engine/classes/Player){ .md-button .md-button--primary }

## Overview

Every connected player has a `Player` instance under the `Players` service. The `Player` object provides access to the player's character, user info, and game settings.

=== "C#"

    ```csharp
    var players = game.GetService<Players>();

    players.PlayerAdded += (player) =>
    {
        print($"{player.DisplayName} ({player.Name}) joined!");
        print($"UserId: {player.UserId}");
    };
    ```

=== "Luau"

    ```lua
    local Players = game:GetService("Players")

    Players.PlayerAdded:Connect(function(player)
        print(`{player.DisplayName} ({player.Name}) joined!`)
        print(`UserId: {player.UserId}`)
    end)
    ```

## Properties

### Identity

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | The player's username |
| `DisplayName` | `string` | The player's display name |
| `UserId` | `double` | Unique user ID |
| `AccountAge` | `double` | Account age in days |
| `HasVerifiedBadge` | `bool` | Whether the player has a verified badge |
| `MembershipType` | `MembershipType` | Premium membership status |
| `LocaleId` | `string` | The player's locale (e.g. `"en-us"`) |

### Character

| Property | Type | Description |
|----------|------|-------------|
| `Character` | `Model` | The player's character model (or null if not spawned) |
| `CharacterAppearanceId` | `double` | User ID to load character appearance from |
| `CanLoadCharacterAppearance` | `bool` | Whether the character appearance loads automatically |
| `RespawnLocation` | `SpawnLocation` | Where the player respawns |

### Camera & Controls

| Property | Type | Description |
|----------|------|-------------|
| `CameraMode` | `CameraMode` | Classic or LockFirstPerson |
| `CameraMaxZoomDistance` | `double` | Max camera zoom distance |
| `CameraMinZoomDistance` | `double` | Min camera zoom distance |
| `AutoJumpEnabled` | `bool` | Whether auto-jump is enabled (mobile) |
| `DevEnableMouseLock` | `bool` | Whether mouse lock (Shift Lock) is allowed |
| `DevComputerMovementMode` | `DevComputerMovementMode` | Movement mode on desktop |
| `DevTouchMovementMode` | `DevTouchMovementMode` | Movement mode on mobile |

### Team

| Property | Type | Description |
|----------|------|-------------|
| `Team` | `Team` | The player's team |
| `TeamColor` | `BrickColor` | The player's team color |
| `Neutral` | `bool` | Whether the player is on a team |

### Display

| Property | Type | Description |
|----------|------|-------------|
| `HealthDisplayDistance` | `double` | Distance at which health bar is visible |
| `NameDisplayDistance` | `double` | Distance at which name is visible |

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Kick(string message)` | `void` | Kicks the player from the game |
| `LoadCharacter()` | `void` | Forces the character to respawn |
| `LoadCharacterWithHumanoidDescription(HumanoidDescription desc)` | `void` | Spawns with a custom appearance |
| `ClearCharacterAppearance()` | `void` | Removes character accessories |
| `Move(Vector3 walkDirection, bool relativeToCamera)` | `void` | Makes the character walk |
| `DistanceFromCharacter(Vector3 point)` | `double` | Distance from character to a point |
| `GetMouse()` | `Mouse` | Returns the player's Mouse object (client only) |
| `GetNetworkPing()` | `double` | Returns the player's network latency |
| `GetRankInGroup(double groupId)` | `double` | Player's rank in a Roblox group |
| `GetRoleInGroup(double groupId)` | `string` | Player's role name in a group |
| `IsFriendsWith(double userId)` | `bool` | Whether the player is friends with a user |
| `IsInGroup(double groupId)` | `bool` | Whether the player is in a group |
| `IsVerified()` | `bool` | Whether the player is ID-verified |
| `HasAppearanceLoaded()` | `bool` | Whether the character appearance has loaded |

Plus all methods inherited from [Instance](instance.md#methods).

## Examples

### Character Spawn Handling

=== "C#"

    ```csharp
    players.PlayerAdded += (player) =>
    {
        player.CharacterAdded += (character) =>
        {
            var humanoid = character.FindFirstChildOfClass("Humanoid");
            print($"{player.Name} spawned with {humanoid.Health} HP");
        };
    };
    ```

=== "Luau"

    ```lua
    Players.PlayerAdded:Connect(function(player)
        player.CharacterAdded:Connect(function(character)
            local humanoid = character:FindFirstChildOfClass("Humanoid")
            print(`{player.Name} spawned with {humanoid.Health} HP`)
        end)
    end)
    ```

### Kicking a Player

=== "C#"

    ```csharp
    player.Kick("You have been removed from the game.");
    ```

=== "Luau"

    ```lua
    player:Kick("You have been removed from the game.")
    ```
