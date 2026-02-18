using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LUSharpTranspiler.Runtime.Internal;
using LUSharpTranspiler.Runtime.STL.Classes;
using LUSharpTranspiler.Runtime.STL.Classes.Instance;
using LUSharpTranspiler.Runtime.STL.Classes.Instance.PVInstance;
using LUSharpTranspiler.Runtime.STL.LuaToC;
using LUSharpTranspiler.Runtime.STL.Services;

namespace LUSharpTranspiler.TestInput.Client
{
    public class Player : ModuleScript
    {
        public static List<Player> players = new()
        {
            new Player("Table", 100),
            new Player("Player2", 80),
            new Player("Player3", 60)
        };
        private static string SomeField { get; set; } = "Default value";

        private static Dictionary<string, object> dictionary = new()
        {
            {"Key1", "Value1" },
            { "Key2", 42 },
            { "Key3", true },
            { "Key4", new object[]{ "nested value", 123, new object[] { new object[] { "nested value", new object[] { "nested value", 123, false }, false }, 123, new object[] { "nested value", 123, false } } } }
        };

        private List<string> listofdata = new()
        {
            "asdf",
            "afsdf",
            "asdfasdfasdf",
            "what"
        };

        public string Name { get; set; } = "DefaultName";
        public int Health { get; set; } = 100;
        public string SomeProperty { get; set; } = "blue";


        public Player(string name, int health)
        {
            Name = name;
            Health = health;

            dictionary.Add(Name, Health);
        }
    }
    public class Main : RobloxScript
    {


        public override void GameEntry()
        {
            var part2 = Instance.New("Part", game.Workspace);
            part2.Name = "LOSER";
            var part = Instance.New<Part>();
            part.Name = "NewPart" + 1;

            var playersService = game.GetService<Players>();

            playersService.PlayerAdded += (LUSharpTranspiler.Runtime.STL.Classes.Instance.Player player) => {
                LuaGlobals.print("A player has joined: " + player.Name);
            };

            playersService.PlayerAdded.Connect((LUSharpTranspiler.Runtime.STL.Classes.Instance.Player player) => {
                LuaGlobals.print("A player has joined: " + player.Name);
            });

            playersService.PlayerRemoving.Connect((player, reason) => {
                LuaGlobals.print("A player has left: " + player.Name);
            });

            LuaGlobals.print("The local player is named: " + playersService.LocalPlayer.Name);
        }
    }
}
