# Vector3

A 3D vector representing a position, direction, or size in 3D space. Vector3 is one of the most commonly used types in Roblox development.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `new Vector3(double x, double y, double z)` | Creates a Vector3 from X, Y, Z components |
| `Vector3.FromNormalId(NormalId normalId)` | Creates a unit Vector3 from a face normal |
| `Vector3.FromAxis(Axis axis)` | Creates a unit Vector3 from an axis enum |

=== "C#"

    ```csharp
    var position = new Vector3(10, 5, -3);
    var direction = new Vector3(0, 1, 0);
    var size = new Vector3(4, 1, 4);
    ```

=== "Luau"

    ```lua
    local position = Vector3.new(10, 5, -3)
    local direction = Vector3.new(0, 1, 0)
    local size = Vector3.new(4, 1, 4)
    ```

## Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `Vector3.zero` | `(0, 0, 0)` | The zero vector |
| `Vector3.one` | `(1, 1, 1)` | The unit vector |
| `Vector3.xAxis` | `(1, 0, 0)` | Unit vector along the X axis |
| `Vector3.yAxis` | `(0, 1, 0)` | Unit vector along the Y axis |
| `Vector3.zAxis` | `(0, 0, 1)` | Unit vector along the Z axis |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `X` | `double` | The X component |
| `Y` | `double` | The Y component |
| `Z` | `double` | The Z component |
| `Magnitude` | `double` | The length of the vector (`sqrt(X² + Y² + Z²)`) |
| `Unit` | `Vector3` | A normalized copy (length = 1) pointing in the same direction |

## Methods

### Math Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `Abs()` | `Vector3` | Returns a vector with absolute values of each component |
| `Ceil()` | `Vector3` | Rounds each component up to the nearest integer |
| `Floor()` | `Vector3` | Rounds each component down to the nearest integer |
| `Sign()` | `Vector3` | Returns the sign (-1, 0, or 1) of each component |
| `Max(Vector3 vector)` | `Vector3` | Component-wise maximum |
| `Min(Vector3 vector)` | `Vector3` | Component-wise minimum |

### Vector Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `Cross(Vector3 other)` | `Vector3` | Cross product — returns a vector perpendicular to both |
| `Dot(Vector3 other)` | `double` | Dot product — returns the cosine similarity scaled by magnitudes |
| `Angle(Vector3 other, Vector3 axis)` | `double` | Angle between two vectors in radians |
| `Lerp(Vector3 goal, double alpha)` | `Vector3` | Linear interpolation between this vector and goal |
| `FuzzyEq(Vector3 other, double epsilon)` | `bool` | Whether two vectors are approximately equal |

## Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `+` | Component-wise addition | `v1 + v2` |
| `-` | Component-wise subtraction | `v1 - v2` |
| `*` | Component-wise multiplication | `v1 * v2` |
| `/` | Component-wise division | `v1 / v2` |

=== "C#"

    ```csharp
    var a = new Vector3(1, 2, 3);
    var b = new Vector3(4, 5, 6);

    var sum = a + b;           // (5, 7, 9)
    var diff = a - b;          // (-3, -3, -3)
    var product = a * b;       // (4, 10, 18)
    var quotient = a / b;      // (0.25, 0.4, 0.5)
    ```

=== "Luau"

    ```lua
    local a = Vector3.new(1, 2, 3)
    local b = Vector3.new(4, 5, 6)

    local sum = a + b           -- (5, 7, 9)
    local diff = a - b          -- (-3, -3, -3)
    local product = a * b       -- (4, 10, 18)
    local quotient = a / b      -- (0.25, 0.4, 0.5)
    ```

## Examples

### Distance Between Two Points

=== "C#"

    ```csharp
    var pointA = new Vector3(10, 0, 5);
    var pointB = new Vector3(20, 0, 15);
    double distance = (pointA - pointB).Magnitude;
    print($"Distance: {distance}");
    ```

=== "Luau"

    ```lua
    local pointA = Vector3.new(10, 0, 5)
    local pointB = Vector3.new(20, 0, 15)
    local distance = (pointA - pointB).Magnitude
    print(`Distance: {distance}`)
    ```

### Normalized Direction

=== "C#"

    ```csharp
    var from = new Vector3(0, 0, 0);
    var to = new Vector3(10, 5, 10);
    var direction = (to - from).Unit;
    print($"Direction: {direction}");
    ```

=== "Luau"

    ```lua
    local from = Vector3.new(0, 0, 0)
    local to = Vector3.new(10, 5, 10)
    local direction = (to - from).Unit
    print(`Direction: {direction}`)
    ```

### Smooth Movement with Lerp

=== "C#"

    ```csharp
    var start = new Vector3(0, 10, 0);
    var goal = new Vector3(50, 10, 50);

    // Move 30% of the way
    var position = start.Lerp(goal, 0.3);
    // position = (15, 10, 15)
    ```

=== "Luau"

    ```lua
    local start = Vector3.new(0, 10, 0)
    local goal = Vector3.new(50, 10, 50)

    local position = start:Lerp(goal, 0.3)
    -- position = (15, 10, 15)
    ```

### Cross Product for Perpendicular Vector

=== "C#"

    ```csharp
    var forward = new Vector3(0, 0, -1);
    var up = new Vector3(0, 1, 0);
    var right = forward.Cross(up);
    // right = (1, 0, 0)
    ```

=== "Luau"

    ```lua
    local forward = Vector3.new(0, 0, -1)
    local up = Vector3.new(0, 1, 0)
    local right = forward:Cross(up)
    -- right = (1, 0, 0)
    ```
