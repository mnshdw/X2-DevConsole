using System;
using System.Collections.Generic;
using System.Linq;
using DevConsole.Runtime.Commands;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime
{
    public readonly struct Command
    {
        public readonly string Name;
        public readonly string Help;
        public readonly Action<string[], DevConsoleHost> Handler;

        public Command(string name, string help, Action<string[], DevConsoleHost> handler)
        {
            Name = name;
            Help = help;
            Handler = handler;
        }
    }

    public static class CommandRegistry
    {
        private static readonly Dictionary<string, Command> Commands = new(
            StringComparer.OrdinalIgnoreCase
        );

        public static void Register(
            string name,
            string help,
            Action<string[], DevConsoleHost> handler
        )
        {
            Commands[name] = new Command(name, help, handler);
        }

        public static void Execute(string line, DevConsoleHost host)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var tokens = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var name = tokens[0];
            var args = tokens.Skip(1).ToArray();

            if (!Commands.TryGetValue(name, out var cmd))
            {
                host.AppendLine($"unknown command: {name} (try 'help')");
                return;
            }

            try
            {
                cmd.Handler(args, host);
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} command '{name}' threw: {ex}");
                host.AppendLine($"[ERR] {ex.GetType().Name}: {ex.Message}");
            }
        }

        public static void RegisterBuiltins()
        {
            Register(
                "help",
                "help - list registered commands",
                (_, host) =>
                {
                    foreach (var c in Commands.Values.OrderBy(c => c.Name))
                    {
                        host.AppendLine($"  {c.Help}");
                    }
                }
            );

            Register(
                "clear",
                "clear - empty the console",
                (_, host) =>
                {
                    host.Clear();
                }
            );

            BuiltinCommands.RegisterAll();
        }
    }
}
