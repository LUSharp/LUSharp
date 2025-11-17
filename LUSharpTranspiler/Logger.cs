using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler
{
    public static class Logger
    {
        public enum LogSeverity
        {
            Verbose,
            Info,
            Warning,
            Error,
        }
        /// <summary>
        /// Log a message to the console with a specific severity level.
        /// </summary>
        /// <param name="type">The log severity. Default is <seealso cref="LogSeverity.Info"/></param>
        /// <param name="param">Any data you want sent, will call the <code>object.ToString()</code> override.</param>
        public static void Log(LogSeverity type = LogSeverity.Info, params object[] param)
        {
            string fmt = "";

            var orig = Console.ForegroundColor;
            switch(type)
            {
                case LogSeverity.Error:
                    {
                        // add the error excmation mark before the message
                        fmt = $"[!] {DateTime.Now}] ";
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    }
                case LogSeverity.Warning:
                    {
                        // add the warning triangle before the message
                        fmt = $"[W] {DateTime.Now}] ";
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    }
                case LogSeverity.Info:
                    {
                        fmt = $"[I] {DateTime.Now}] ";
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                    }
                case LogSeverity.Verbose:
                    {
                        fmt = $"[🔍] {DateTime.Now}] ";
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    }
            }

            foreach (var parameter in param)
            {
                fmt += parameter.ToString();
            }

            Console.WriteLine(fmt);
            Console.ForegroundColor = orig; // reset to original color
        }
        /// <summary>
        /// Override for <seealso cref="Log(LogSeverity, object[])"/> with default severity of <seealso cref="LogSeverity.Info"/>
        /// </summary>
        /// <param name="param">Any data you want sent, will call the <code>object.ToString()</code> override.</param>
        public static void Log(params object[] param)
        {
            Log(LogSeverity.Info, param);
        }

        public static void LogException(params object[] param)
        {
            string fmt = "";

            // add the error excmation mark before the message
            fmt = $"[!] {DateTime.Now}] ";

            foreach (var parameter in param)
            {
                fmt += parameter.ToString();
            }

            throw new Exception(fmt, new Exception(new StackTrace().ToString()));
        }

    }
}
