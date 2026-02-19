using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LUSharpAPI.Runtime.Internal;
using LUSharpAPI.Runtime.STL.Classes;
using LUSharpAPI.Runtime.STL.Classes.Instance;
using LUSharpAPI.Runtime.STL.Classes.Instance.PVInstance;
using LUSharpAPI.Runtime.STL.Services;

namespace LUSharpTranspiler.TestInput.Client
{
    public class CPlayer : ModuleScript
    {
        public static List<CPlayer> players = new()
        {
            new CPlayer("Table", 100),
            new CPlayer("Player2", 80),
            new CPlayer("Player3", 60)
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


        public CPlayer(string name, int health)
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

            playersService.PlayerAdded += (Player player) => {
                print("A player has joined: " + player.Name);
            };

            playersService.PlayerAdded.Connect((Player player) => {
                print("A player has joined: " + player.Name);
            });

            playersService.PlayerRemoving.Connect((player, reason) => {
                print("A player has left: " + player.Name);
            });

            print("The local player is named: " + playersService.LocalPlayer.Name);
        }
    }
}
