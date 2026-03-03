using System;
using System.Collections.Generic;
using Roblox.Classes;


namespace Game.Server
{
    public class NewScript 
	{
        public enum SomeEnum
        {
            State1,
            State2,
            State3
        }
        public struct SomeStruct
        {
            public string Field1;
            public int Field2;
        }
		NewScript2 newScript = new();
    	public static void Main() 
    	{
            SomeStruct testStruct = new()
            {
                Field1 = "some text",
                Field2 = 5
            };
            
            Console.WriteLine($"Hello World: {testStruct.Field1}");
			NewScript2.SomeFunc2();
			var items = new List<int>() {3,35,52,5,235,25,23,523,52,35,235};
            items[0] = 5;
			foreach(var item in items)
			{
		   	 print(item);
				error();
			}

			var players = game.GetService<Players>();
			players.PlayerAdded.ConnectParallel((Player p) => 
			{
				print($"{p.DisplayName} has joined the game!");
			});
    	}

		public static void SomeFunc()
		{
			print("somefunc called in newscript");
		}
	}
}