# Events (RBXScriptSignal)

The Roblox event system uses `RBXScriptSignal` for event signals and `RBXScriptConnection` for managing listener subscriptions. In LUSharp, you connect to events using C#'s `+=` operator, which transpiles to `:Connect()`.

[:octicons-link-external-16: Roblox API Reference — RBXScriptSignal](https://create.roblox.com/docs/reference/engine/datatypes/RBXScriptSignal){ .md-button .md-button--primary }

## Connecting to Events

LUSharp supports two syntax styles for connecting to events:

=== "C# (operator syntax)"

    ```csharp
    // += operator — transpiles to :Connect()
    part.Touched += (otherPart) =>
    {
        print($"Touched by {otherPart.Name}");
    };
    ```

=== "C# (method syntax)"

    ```csharp
    // .Connect() — direct call
    part.Touched.Connect((otherPart) =>
    {
        print($"Touched by {otherPart.Name}");
    });
    ```

=== "Luau"

    ```lua
    part.Touched:Connect(function(otherPart)
        print(`Touched by {otherPart.Name}`)
    end)
    ```

## RBXScriptSignal

The base event signal type. Comes in generic variants for typed parameters.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Connect(Action handler)` | `RBXScriptConnection` | Connects a handler that fires every time the event triggers |
| `ConnectParallel(Action handler)` | `RBXScriptConnection` | Connects a handler that runs in a desynchronized (parallel) state |
| `Once(Action handler)` | `RBXScriptConnection` | Connects a handler that fires only once, then auto-disconnects |
| `Wait()` | `object[]` | Yields the current thread until the event fires, returns the arguments |

### Generic Variants

| Type | Description |
|------|-------------|
| `RBXScriptSignal` | No parameters |
| `RBXScriptSignal<T1>` | One typed parameter |
| `RBXScriptSignal<T1, T2>` | Two typed parameters |

## RBXScriptConnection

Represents an active event connection. Use it to disconnect a listener.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Connected` | `bool` | Whether the connection is still active |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Disconnect()` | `void` | Stops the handler from being called |

## Examples

### Once (Fire and Forget)

=== "C#"

    ```csharp
    // Only handle the first player who joins
    players.PlayerAdded.Once((player) =>
    {
        print($"First player: {player.Name}");
    });
    ```

=== "Luau"

    ```lua
    Players.PlayerAdded:Once(function(player)
        print(`First player: {player.Name}`)
    end)
    ```

### Wait (Yielding)

=== "C#"

    ```csharp
    // Wait for a player to join before continuing
    var args = players.PlayerAdded.Wait();
    print("A player has joined!");
    ```

=== "Luau"

    ```lua
    local player = Players.PlayerAdded:Wait()
    print("A player has joined!")
    ```

### Disconnecting

=== "C#"

    ```csharp
    var connection = part.Touched.Connect((otherPart) =>
    {
        print("Touched!");
    });

    // Later, disconnect it
    connection.Disconnect();
    ```

=== "Luau"

    ```lua
    local connection = part.Touched:Connect(function(otherPart)
        print("Touched!")
    end)

    connection:Disconnect()
    ```

## Common Events

| Object | Event | Parameters | Description |
|--------|-------|------------|-------------|
| `Instance` | `ChildAdded` | `Instance` | A child was added |
| `Instance` | `ChildRemoved` | `Instance` | A child was removed |
| `Instance` | `Destroying` | — | The instance is being destroyed |
| `BasePart` | `Touched` | `BasePart` | Another part touched this one |
| `BasePart` | `TouchEnded` | `BasePart` | Another part stopped touching |
| `Players` | `PlayerAdded` | `Player` | A player joined |
| `Players` | `PlayerRemoving` | `Player, PlayerExitReason` | A player is leaving |

For the full list of events on each class, see the individual API reference pages.
