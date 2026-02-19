# BasePart

```
Instance > PVInstance > BasePart
```

[:octicons-link-external-16: Roblox API Reference](https://create.roblox.com/docs/reference/engine/classes/BasePart){ .md-button .md-button--primary }

The base class for all physical parts in Roblox — `Part`, `MeshPart`, `WedgePart`, and others. BasePart provides properties for position, size, appearance, physics, and collision.

## Overview

BasePart is the most commonly used class in Roblox development. It represents any physical object in the 3D world with a position, size, and material.

=== "C#"

    ```csharp
    var part = Instance.New<Part>(workspace);
    part.Size = new Vector3(4, 1, 4);
    part.Position = new Vector3(0, 10, 0);
    part.Anchored = true;
    part.Color = Color3.FromRGB(255, 0, 0);
    part.Material = Material.Neon;
    ```

=== "Luau"

    ```lua
    local part = Instance.new("Part")
    part.Parent = workspace
    part.Size = Vector3.new(4, 1, 4)
    part.Position = Vector3.new(0, 10, 0)
    part.Anchored = true
    part.Color = Color3.fromRGB(255, 0, 0)
    part.Material = Enum.Material.Neon
    ```

## Properties

### Transform

| Property | Type | Description |
|----------|------|-------------|
| `CFrame` | `CFrame` | Position and orientation as a coordinate frame |
| `Position` | `Vector3` | Position in world space |
| `Orientation` | `Vector3` | Rotation in degrees (X, Y, Z) |
| `Rotation` | `Vector3` | Rotation in degrees |
| `Size` | `Vector3` | Size in studs (X, Y, Z) |
| `PivotOffset` | `CFrame` | Offset of the pivot from the CFrame |
| `ExtentsCFrame` | `CFrame` | CFrame of the bounding box |
| `ExtentsSize` | `Vector3` | Size of the bounding box |

### Physics

| Property | Type | Description |
|----------|------|-------------|
| `Anchored` | `bool` | Whether the part is immovable by physics |
| `Mass` | `double` | *(Read-only)* The mass of the part |
| `Massless` | `bool` | Whether the part contributes to assembly mass |
| `AssemblyMass` | `double` | *(Read-only)* Total mass of the physics assembly |
| `AssemblyRootPart` | `BasePart` | *(Read-only)* Root part of the physics assembly |
| `AssemblyAngularVelocity` | `Vector3` | Angular velocity of the assembly |
| `AssemblyLinearVelocity` | `Vector3` | Linear velocity of the assembly |
| `AssemblyCenterOfMass` | `Vector3` | *(Read-only)* Center of mass of the assembly |
| `CenterOfMass` | `Vector3` | *(Read-only)* Center of mass of this part |
| `RootPriority` | `double` | Priority for becoming the assembly root |
| `CurrentPhysicalProperties` | `PhysicalProperties` | *(Read-only)* Current physical properties |
| `CustomPhysicalProperties` | `PhysicalProperties` | Custom physical properties override |
| `EnableFluidForces` | `bool` | Whether aerodynamic forces apply |

### Collision

| Property | Type | Description |
|----------|------|-------------|
| `CanCollide` | `bool` | Whether other parts physically collide with this part |
| `CanQuery` | `bool` | Whether spatial queries (raycasts, etc.) detect this part |
| `CanTouch` | `bool` | Whether Touched events fire for this part |
| `AudioCanCollide` | `bool` | Whether audio collision events fire |
| `CollisionGroup` | `string` | The collision group this part belongs to |

### Appearance

| Property | Type | Description |
|----------|------|-------------|
| `Color` | `Color3` | The RGB color of the part |
| `BrickColor` | `BrickColor` | The legacy BrickColor of the part |
| `Material` | `Material` | The material (Plastic, Neon, Wood, etc.) |
| `MaterialVariant` | `string` | Custom material variant name |
| `Transparency` | `double` | Transparency (0 = opaque, 1 = invisible) |
| `Reflectance` | `double` | How much the part reflects (0–1) |
| `CastShadow` | `bool` | Whether the part casts shadows |
| `LocalTransparencyModifier` | `double` | Client-only transparency modifier |

### Surface

| Property | Type | Description |
|----------|------|-------------|
| `TopSurface` | `SurfaceType` | Surface type of the +Y face |
| `BottomSurface` | `SurfaceType` | Surface type of the -Y face |
| `FrontSurface` | `SurfaceType` | Surface type of the -Z face |
| `BackSurface` | `SurfaceType` | Surface type of the +Z face |
| `LeftSurface` | `SurfaceType` | Surface type of the -X face |
| `RightSurface` | `SurfaceType` | Surface type of the +X face |

### Other

| Property | Type | Description |
|----------|------|-------------|
| `Locked` | `bool` | Whether the part is selectable in Studio |
| `ReceiveAge` | `double` | Time since the part was last updated from the server |
| `ResizeIncrement` | `double` | *(Read-only)* Increment for Studio resize handles |
| `ResizeableFaces` | `Faces` | *(Read-only)* Which faces can be resized in Studio |

## Methods

### Physics

| Method | Returns | Description |
|--------|---------|-------------|
| `ApplyImpulse(Vector3 impulse)` | `void` | Applies an impulse force at the center of mass |
| `ApplyImpulseAtPosition(Vector3 impulse, Vector3 position)` | `void` | Applies an impulse force at a world position |
| `ApplyAngularImpulse(Vector3 impulse)` | `void` | Applies an angular impulse (torque) |
| `GetMass()` | `double` | Returns the mass of the part |
| `GetVelocityAtPosition(Vector3 position)` | `Vector3` | Returns velocity at a world point |
| `IsGrounded()` | `bool` | Whether the part's assembly is anchored |
| `AngularAccelerationToTorque(Vector3 angAccel, Vector3 angVelocity)` | `Vector3` | Converts angular acceleration to torque |
| `TorqueToAngularAcceleration(Vector3 torque, Vector3 angVelocity)` | `Vector3` | Converts torque to angular acceleration |

### Collision

| Method | Returns | Description |
|--------|---------|-------------|
| `CanCollideWith(BasePart otherPart)` | `bool` | Whether this part can collide with another |
| `GetTouchingParts()` | `List<Instance>` | Returns parts currently touching this one |
| `GetConnectedParts(bool recursive)` | `List<Instance>` | Returns parts connected by joints |
| `GetJoints()` | `List<Instance>` | Returns all joints attached to this part |
| `GetNoCollisionConstaints()` | `List<Instance>` | Returns NoCollisionConstraints |
| `GetClosestPointOnSurface(Vector3 position)` | `Vector3` | Returns the closest point on the part's surface |

### Network Ownership

| Method | Returns | Description |
|--------|---------|-------------|
| `GetNetworkOwner()` | `Instance` | Returns the player who owns this part's physics |
| `SetNetworkOwner(Player player)` | `void` | Sets the network owner |
| `GetNetworkOwnershipAuto()` | `bool` | Whether ownership is set automatically |
| `SetNetworkOwnershipAuto()` | `void` | Enables automatic network ownership |
| `CanSetNetworkOwnership()` | `(bool, string)` | Whether ownership can be set |

### CSG Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `UnionAsync(List<Instance> parts, ...)` | `Instance` | Creates a union of parts |
| `IntersectAsync(List<Instance> parts, ...)` | `Instance` | Creates an intersection of parts |
| `SubtractAsync(List<Instance> parts, ...)` | `Instance` | Subtracts parts from this one |

### Resize

| Method | Returns | Description |
|--------|---------|-------------|
| `Resize(NormalId normalId, double deltaAmount)` | `bool` | Resizes the part along a face |

## Inherited Methods (from PVInstance)

| Method | Returns | Description |
|--------|---------|-------------|
| `GetPivot()` | `CFrame` | Returns the pivot CFrame |
| `PivotTo(CFrame targetCFrame)` | `void` | Moves the model/part to align its pivot with the target |

## Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `Touched` | `BasePart otherPart` | Fires when another part touches this one |
| `TouchEnded` | `BasePart otherPart` | Fires when another part stops touching this one |

Plus all events inherited from [Instance](instance.md#events).

## Examples

### Creating a Part

=== "C#"

    ```csharp
    var part = Instance.New<Part>(workspace);
    part.Name = "Platform";
    part.Size = new Vector3(20, 1, 20);
    part.Position = new Vector3(0, 5, 0);
    part.Anchored = true;
    part.Material = Material.SmoothPlastic;
    part.Color = Color3.FromRGB(100, 200, 100);
    ```

=== "Luau"

    ```lua
    local part = Instance.new("Part")
    part.Parent = workspace
    part.Name = "Platform"
    part.Size = Vector3.new(20, 1, 20)
    part.Position = Vector3.new(0, 5, 0)
    part.Anchored = true
    part.Material = Enum.Material.SmoothPlastic
    part.Color = Color3.fromRGB(100, 200, 100)
    ```

### Touch Detection

=== "C#"

    ```csharp
    var killBrick = workspace.FindFirstChild("KillBrick") as BasePart;

    killBrick.Touched += (otherPart) =>
    {
        var humanoid = otherPart.Parent?.FindFirstChildOfClass("Humanoid");
        if (humanoid != null)
        {
            humanoid.Health = 0;
            print($"{otherPart.Parent.Name} was eliminated!");
        }
    };
    ```

=== "Luau"

    ```lua
    local killBrick = workspace:FindFirstChild("KillBrick")

    killBrick.Touched:Connect(function(otherPart)
        local humanoid = otherPart.Parent
            and otherPart.Parent:FindFirstChildOfClass("Humanoid")
        if humanoid then
            humanoid.Health = 0
            print(`{otherPart.Parent.Name} was eliminated!`)
        end
    end)
    ```

### Applying Physics

=== "C#"

    ```csharp
    var ball = Instance.New<Part>(workspace);
    ball.Shape = PartType.Ball;
    ball.Size = new Vector3(2, 2, 2);
    ball.Position = new Vector3(0, 20, 0);

    // Launch it forward
    ball.ApplyImpulse(new Vector3(0, 50, -100));
    ```

=== "Luau"

    ```lua
    local ball = Instance.new("Part")
    ball.Parent = workspace
    ball.Shape = Enum.PartType.Ball
    ball.Size = Vector3.new(2, 2, 2)
    ball.Position = Vector3.new(0, 20, 0)

    ball:ApplyImpulse(Vector3.new(0, 50, -100))
    ```
