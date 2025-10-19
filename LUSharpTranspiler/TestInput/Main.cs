using System;

namespace LUSharpTranspiler.TestInput
{
    public class Player
    {
        private static List<string> s_Instances = new()
        {
            "First",
            "Second",
            "Third"
        };

        public string Name { get; set; }
        public string Health { get; set; }

        private static string SomeField { get; set; } = "Default value";

        public Player(string name, string health)
        {
            this.Name = name;
            this.Health = health;

            s_Instances.Add(this.Name);
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
