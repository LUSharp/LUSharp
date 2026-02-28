using System;
using System.Collections.Generic;

namespace Game.Server
{
    public class NewScript 
	{
		NewScript2 newScript = new();
    	public static void Main() 
    	{
			Console.WriteLine("Hello World");
			NewScript2.SomeFunc2();

			var items = new List<int>() {3,35,52,5,235,25,23,523,52,35,235};
			foreach(var item in items)
			{
		   	 print(item);
			}

			
			var players = game.GetService("Players");
			players.PlayerAdded.ConnectParallel(async (Player p) => 
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