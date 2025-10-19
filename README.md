# 🦈 LUSharp

**Write Roblox games in C# — transpiled to Luau.**  
LUSharp brings the power, structure, and safety of C# to Roblox development. Build scalable Roblox experiences using modern C# syntax and features — then let LUSharp handle converting your code to Luau automatically.

---

## 🚀 Overview

**LUSharp** is a C# → Luau transpiler designed for Roblox developers who want:
- **Strong typing** and **intellisense** while coding.  
- **Familiar C# OOP patterns** instead of vanilla Lua syntax.  
- **Full Roblox API support**, including services, instances, and remote events.  
- **Easy project integration** with tools like [Rojo](https://rojo.space) or [Roblox Studio].  

If you’ve used **roblox-ts**, you already get the idea — LUSharp gives you the same power, but for **C# developers**.

---

## ✨ Features

✅ Write idiomatic C# — classes, methods, properties, events, and more  
✅ Private/public variable support (transpiled safely to Luau)  
✅ Automatic module generation for Roblox environments  
✅ Type-safe remote events and function wrappers  
✅ Optional custom APIs for faster development  
✅ Seamless debugging and runtime type hints  

---

## 🧩 Example

### C# Input
```csharp
public class Player
{
    private int health = 100;
    private string name;

    public Player(string name)
    {
        this.name = name;
    }

    public void Damage(int amount)
    {
        health -= amount;
    }

    public int GetHealth()
    {
        return health;
    }
}
```

### Generated Luau
```lua
local Player = {}

function Player.new(name)
    local private = {
        health = 100,
        name = name
    }

    return private
end

function Player.Damage(self, amount)
    self.health -= amount
end

function Player.GetHealth(self)
    return self.health
end

return Player
```

---

## ⚙️ Getting Started

1. **Clone the repo**
   ```bash
   git clone https://github.com/yourusername/LUSharp.git
   cd LUSharp
   ```

2. **Build the transpiler**
   ```bash
   dotnet build
   ```

3. **Transpile your C# project**
   ```bash
   lusharp build
   ```

4. **Sync to Roblox Studio**
   - Use **Rojo** or your preferred method to push the generated Luau files into your game.

---

## 🧠 How It Works

LUSharp parses your C# syntax trees and reconstructs the logic into valid Luau equivalents.  
It automatically:
- Converts C# classes → Luau module scripts  
- Converts methods → Luau functions  
- Converts fields/properties → local or table entries  
- Preserves method overloading and scoping as much as possible within Luau’s constraints  

---

## 🛠️ Roadmap

- [ ] Support for enums and interfaces  
- [ ] Asynchronous constructs (`async` / `await` → coroutines)  
- [ ] Unity-style API extensions  
- [ ] Editor plugin for VSCode and Rider  
- [ ] Source map debugging in Roblox Studio  

---

## 💬 Community & Contributing

Contributions are welcome!  
If you find a bug, want to suggest features, or help shape the direction of LUSharp, feel free to open a PR or issue.

Join the discussions, share snippets, and help build the **next-gen Roblox development experience**.

---

## 📄 License

LUSharp is licensed under the [MIT License](LICENSE).
