# Instance

```
RObject > Instance
```

The base class for all objects in the Roblox hierarchy. Every object in a Roblox game — parts, models, scripts, UI elements — is an `Instance`.

## Overview

`Instance` provides the core functionality shared by all Roblox objects: naming, parenting, tagging, attributes, and hierarchy traversal. All other Roblox classes inherit from `Instance`.

=== "C#"

    ```csharp
    var part = Instance.New<Part>();
    part.Name = "MyPart";
    part.Parent = workspace;

    var found = workspace.FindFirstChild("MyPart");
    print(found.Name); // "MyPart"
    ```

=== "Luau"

    ```lua
    local part = Instance.new("Part")
    part.Name = "MyPart"
    part.Parent = workspace

    local found = workspace:FindFirstChild("MyPart")
    print(found.Name) -- "MyPart"
    ```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Archivable` | `bool` | Whether the instance can be cloned and saved/published |
| `Capabilities` | `SecurityCapability` | Capabilities allowed for scripts inside this container |
| `Name` | `string` | A non-unique identifier for the instance |
| `Parent` | `Instance` | The hierarchical parent of the instance |
| `RobloxLocked` | `bool` | *(Deprecated)* Used to protect CoreGui objects |
| `Sandboxed` | `bool` | Whether the instance is a sandboxed container |

## Methods

### Hierarchy

| Method | Returns | Description |
|--------|---------|-------------|
| `FindFirstChild(string name)` | `Instance` | Returns the first child with the given name, or `null` |
| `FindFirstChildOfClass(string className)` | `Instance` | Returns the first child of the given class |
| `FindFirstChildWhichIsA(string className)` | `Instance` | Returns the first child that inherits from the given class |
| `FindFirstDescendant(string name)` | `Instance` | Searches all descendants for the given name |
| `FindFirstAncestor(string name)` | `Instance` | Returns the first ancestor with the given name |
| `FindFirstAncestorOfClass(string className)` | `Instance` | Returns the first ancestor of the given class |
| `FindFirstAncestorWhichIsA(string className)` | `Instance` | Returns the first ancestor inheriting from the given class |
| `WaitForChild(string childName, int timeOut)` | `Instance` | Yields until a child with the given name exists |
| `GetChildren()` | `Instance[]` | Returns an array of all direct children |
| `GetDescendants()` | `Instance[]` | Returns an array of all descendants |
| `GetFullName()` | `string` | Returns the full path (e.g. `"Workspace.Model.Part"`) |
| `IsAncestorOf(Instance descendant)` | `bool` | Whether this is an ancestor of the given instance |
| `IsDescendantOf(Instance ancestor)` | `bool` | Whether this is a descendant of the given instance |

### Lifecycle

| Method | Returns | Description |
|--------|---------|-------------|
| `Clone()` | `Instance` | Creates a deep copy of the instance and its descendants |
| `Destroy()` | `void` | Removes the instance and all descendants |
| `ClearAllChildren()` | `void` | Destroys all children |

### Tags

| Method | Returns | Description |
|--------|---------|-------------|
| `AddTag(string tag)` | `void` | Adds a tag to the instance |
| `RemoveTag(string tag)` | `void` | Removes a tag from the instance |
| `HasTag(string tag)` | `bool` | Whether the instance has the given tag |
| `GetTags()` | `string[]` | Returns all tags on the instance |

### Attributes

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAttribute(string attribute)` | `object` | Returns the value of the named attribute |
| `SetAttribute(string attribute, object value)` | `void` | Sets the value of the named attribute |
| `GetAttributes()` | `Dictionary<string, object>` | Returns all attributes as a dictionary |
| `GetAttributeChangedSignal(string attribute)` | `RBXScriptSignal` | Returns a signal that fires when the attribute changes |

### Other

| Method | Returns | Description |
|--------|---------|-------------|
| `GetActor()` | `Actor` | Returns the Actor ancestor, if any |
| `GetStyled(string name)` | `object` | Returns the styled value for a property |
| `GetStyledPropertyChangedSignal(string property)` | `RBXScriptSignal` | Signal for styled property changes |
| `IsPropertyModified(string property)` | `bool` | Whether a property has been modified from default |
| `ResetPropertyToDefault(string property)` | `void` | Resets a property to its default value |
| `QueryDescendants()` | `List<Instance>` | Queries descendants with filters |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Instance.New(string className, RObject? parent)` | `Instance` | Creates a new instance of the given class |
| `Instance.New<T>(RObject? parent)` | `T` | Generic version — creates an instance of type `T` |
| `Instance.fromExisting(RObject existingInstance)` | `Instance` | Wraps an existing Roblox instance |

## Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `AncestryChanged` | `Instance child, Instance parent` | Fires when the instance's ancestry changes |
| `AttributeChanged` | `string attribute` | Fires when any attribute changes |
| `ChildAdded` | `Instance child` | Fires when a child is added |
| `ChildRemoved` | `Instance child` | Fires when a child is removed |
| `DescendantAdded` | `Instance descendant` | Fires when a descendant is added |
| `DescendantRemoving` | `Instance descendant` | Fires when a descendant is about to be removed |
| `Destroying` | *(none)* | Fires when the instance is being destroyed |
| `StyledPropertiesChanged` | *(none)* | Fires when styled properties change |

## Examples

### Finding and Manipulating Children

=== "C#"

    ```csharp
    // Find by name
    var door = workspace.FindFirstChild("Door");
    if (door != null)
    {
        door.Name = "OpenDoor";
    }

    // Find by class
    var camera = workspace.FindFirstChildOfClass("Camera");

    // Wait for a child to exist
    var spawner = workspace.WaitForChild("Spawner", 10);
    ```

=== "Luau"

    ```lua
    local door = workspace:FindFirstChild("Door")
    if door then
        door.Name = "OpenDoor"
    end

    local camera = workspace:FindFirstChildOfClass("Camera")

    local spawner = workspace:WaitForChild("Spawner", 10)
    ```

### Listening to Events

=== "C#"

    ```csharp
    workspace.ChildAdded += (child) =>
    {
        print($"New child: {child.Name}");
    };

    workspace.DescendantAdded += (desc) =>
    {
        if (desc.HasTag("Collectible"))
        {
            print($"Collectible added: {desc.Name}");
        }
    };
    ```

=== "Luau"

    ```lua
    workspace.ChildAdded:Connect(function(child)
        print(`New child: {child.Name}`)
    end)

    workspace.DescendantAdded:Connect(function(desc)
        if desc:HasTag("Collectible") then
            print(`Collectible added: {desc.Name}`)
        end
    end)
    ```

### Using Attributes

=== "C#"

    ```csharp
    var part = Instance.New<Part>(workspace);
    part.SetAttribute("Health", 100);
    part.SetAttribute("Team", "Red");

    int health = (int)part.GetAttribute("Health");
    print($"Health: {health}");
    ```

=== "Luau"

    ```lua
    local part = Instance.new("Part")
    part.Parent = workspace
    part:SetAttribute("Health", 100)
    part:SetAttribute("Team", "Red")

    local health = part:GetAttribute("Health")
    print(`Health: {health}`)
    ```
