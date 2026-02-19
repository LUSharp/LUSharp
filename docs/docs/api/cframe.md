# CFrame

A **Coordinate Frame** representing a position and orientation in 3D space. CFrame is used for positioning and rotating objects, cameras, and other 3D elements.

## Overview

A CFrame combines a 3D position with a 3×3 rotation matrix into a single transform. It is the primary way to position and orient objects in Roblox.

=== "C#"

    ```csharp
    // Position a part at (10, 5, 10) looking at the origin
    var cf = new CFrame(new Vector3(10, 5, 10), Vector3.zero);
    part.CFrame = cf;
    ```

=== "Luau"

    ```lua
    local cf = CFrame.new(Vector3.new(10, 5, 10), Vector3.zero)
    part.CFrame = cf
    ```

## Constructors

| Constructor | Description |
|-------------|-------------|
| `new CFrame()` | Identity CFrame at the origin |
| `new CFrame(Vector3 position)` | CFrame at a position with no rotation |
| `new CFrame(Vector3 position, Vector3 lookAt)` | CFrame at a position looking toward a point |
| `new CFrame(double x, double y, double z)` | CFrame at (x, y, z) with no rotation |
| `new CFrame(x, y, z, qX, qY, qZ, qW)` | CFrame from position + quaternion rotation |
| `new CFrame(x, y, z, R00, R01, R02, R10, R11, R12, R20, R21, R22)` | CFrame from position + rotation matrix components |

### Constructor Examples

=== "C#"

    ```csharp
    // Simple position
    var cf1 = new CFrame(10, 5, 10);

    // Position + look-at target
    var cf2 = new CFrame(
        new Vector3(10, 5, 10),
        new Vector3(0, 0, 0)
    );

    // From Vector3 position only
    var cf3 = new CFrame(new Vector3(10, 5, 10));
    ```

=== "Luau"

    ```lua
    local cf1 = CFrame.new(10, 5, 10)

    local cf2 = CFrame.new(
        Vector3.new(10, 5, 10),
        Vector3.new(0, 0, 0)
    )

    local cf3 = CFrame.new(Vector3.new(10, 5, 10))
    ```

## Factory Methods

| Method | Description |
|--------|-------------|
| `CFrame.lookAt(Vector3 at, Vector3 lookAt, Vector3 up)` | Creates a CFrame at a position looking toward a target with an up vector |
| `CFrame.lookAlong(Vector3 at, Vector3 direction, Vector3 up)` | Creates a CFrame at a position looking along a direction |
| `CFrame.fromRotationBetweenVectors(Vector3 from, Vector3 to)` | Rotation that transforms one vector to another |
| `CFrame.fromEulerAngles(double rx, double ry, double rz, RotationOrder order)` | CFrame from Euler angles with a specified rotation order |
| `CFrame.fromEulerAnglesXYZ(double rx, double ry, double rz)` | CFrame from Euler angles in XYZ order |
| `CFrame.fromEulerAnglesYXZ(double rx, double ry, double rz)` | CFrame from Euler angles in YXZ order |
| `CFrame.Angles(double rx, double ry, double rz)` | Alias for `fromEulerAnglesXYZ` |
| `CFrame.fromOrientation(double rx, double ry, double rz)` | CFrame from orientation angles (same as `fromEulerAnglesYXZ`) |
| `CFrame.fromAxisAngle(Vector3 axis, double angle)` | CFrame rotated around an axis by an angle (radians) |
| `CFrame.fromMatrix(Vector3 pos, Vector3 vX, Vector3 vY, Vector3 vZ)` | CFrame from position + basis vectors |

## Examples

### Positioning a Part

=== "C#"

    ```csharp
    // Set a part's position and rotation
    part.CFrame = new CFrame(0, 10, 0);

    // Make a part face another part
    part.CFrame = new CFrame(part.Position, target.Position);
    ```

=== "Luau"

    ```lua
    part.CFrame = CFrame.new(0, 10, 0)

    part.CFrame = CFrame.new(part.Position, target.Position)
    ```

### Rotating a Part

=== "C#"

    ```csharp
    // Rotate 45 degrees around the Y axis
    double angle = Math.PI / 4; // 45 degrees in radians
    part.CFrame = CFrame.Angles(0, angle, 0);

    // Rotate from Euler angles
    part.CFrame = CFrame.fromEulerAnglesXYZ(0, Math.PI / 2, 0);
    ```

=== "Luau"

    ```lua
    local angle = math.pi / 4
    part.CFrame = CFrame.Angles(0, angle, 0)

    part.CFrame = CFrame.fromEulerAnglesXYZ(0, math.pi / 2, 0)
    ```

### Look-At Camera

=== "C#"

    ```csharp
    var cameraPos = new Vector3(0, 20, 20);
    var target = new Vector3(0, 0, 0);
    var upVector = new Vector3(0, 1, 0);

    var cameraCF = CFrame.lookAt(cameraPos, target, upVector);
    ```

=== "Luau"

    ```lua
    local cameraPos = Vector3.new(0, 20, 20)
    local target = Vector3.new(0, 0, 0)
    local upVector = Vector3.new(0, 1, 0)

    local cameraCF = CFrame.lookAt(cameraPos, target, upVector)
    ```

### Orbiting Around a Point

=== "C#"

    ```csharp
    var center = new Vector3(0, 5, 0);
    double radius = 20;
    double angle = 0;

    // Create a CFrame orbiting the center
    var orbitCF = new CFrame(center)
        * CFrame.Angles(0, angle, 0)
        * new CFrame(0, 0, radius);
    ```

=== "Luau"

    ```lua
    local center = Vector3.new(0, 5, 0)
    local radius = 20
    local angle = 0

    local orbitCF = CFrame.new(center)
        * CFrame.Angles(0, angle, 0)
        * CFrame.new(0, 0, radius)
    ```

!!! tip
    CFrame multiplication (`*`) composes transforms. `A * B` applies transform B in A's local space. This is how you chain translations and rotations.
