using System;

namespace LUSharpTranspiler.TestInput
{
    public class Player
    {
        private static Dictionary<string, object> dictionary = new()
        {
            {"Key1", "Value1" },
            { "Key2", 42 },
            { "Key3", true } 
        };

        private static List<string> balls = new()
        {
            "asdf",
            "afsdf",
            "asdfasdfasdf",
            "what"
        };

        public string Name { get; set; }
        public string Health { get; set; }

        private static string SomeField { get; set; } = "Default value";

        public Player(string name, string health)
        {
            this.Name = name;
            this.Health = health;

            dictionary.Add(this.Name, this.Health);
        }
    }
    public class Main
    {
        public static void GameEntry()
        {
            Console.WriteLine("Hi");
        }
    }
}
