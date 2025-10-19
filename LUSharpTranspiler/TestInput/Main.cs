using System;

namespace LUSharpTranspiler.TestInput
{
    public class Player
    {
        private static int InstanceCount { get; set; } = 5;
        public string Name { get; set; }
        public string Health { get; set; }

        private static string SomeField { get; set; } = "Default value";

        public Player(string name, string health)
        {
            this.Name = name;
            this.Health = health;

            InstanceCount++;
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
