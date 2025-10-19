global using LUSharp;
global using Logger = LUSharp.Logger;
global using LogSeverity = LUSharp.Logger.LogSeverity;
using System;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTranspiler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Array.Resize(ref args, 1);
            args[0] = "C:\\Users\\table\\source\\repos\\LUSharp\\LUSharpTranspiler\\TestInput";

            // Set the title
            Console.Title = "LU# Transpiler";
            // do some fancy ascii art for LU# Transpiler

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                @"               _ _                                            
                 __    _____ _| | |_    _____                     _ _         
                |  |  |  |  |_     _|  |_   _|___ ___ ___ ___ ___|_| |___ ___ 
                |  |__|  |  |_     _|    | | |  _| .'|   |_ -| . | | | -_|  _|
                |_____|_____| |_|_|      |_| |_| |__,|_|_|___|  _|_|_|___|_|  
                                                             |_|              ");
            Console.ForegroundColor = ConsoleColor.Gray;
            // validate input args
            if(args.Length == 0)
            {
                Logger.Log(LogSeverity.Error, "Please provide a project folder to transpile.");
                return;
            }

            string projectPath = args[0];
            if(!Directory.Exists(projectPath))
            {
                Logger.Log(LogSeverity.Error, "The provided project folder does not exist.");
                return;
            }

            DirectoryInfo outputDir = Directory.CreateDirectory($"{projectPath}-transpiled");
            Logger.Log(LogSeverity.Info, $"Transpiling project at {projectPath} »» ./{outputDir.Name}");

            Transpiler.Transpiler.TranspileProject(projectPath, outputDir.FullName);
        }
    }
}
