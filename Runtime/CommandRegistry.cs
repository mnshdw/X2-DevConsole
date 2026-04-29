using System;
using System.Collections.Generic;
using System.Linq;
using DevConsole.Runtime.Commands;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime
{
    [Flags]
    public enum Scene
    {
        Global = 1,
        Geoscape = 2,
        GroundCombat = 4,
    }

    public class HelpEntry
    {
        public string Signature;
        public string[] DescriptionLines;

        public HelpEntry(string signature, string[] descriptionLines)
        {
            Signature = signature;
            DescriptionLines = descriptionLines;
        }
    }

    public class Command
    {
        public string Name;
        public Action<string[], DevConsoleHost> Handler;
        public Dictionary<Scene, HelpEntry> HelpByScene = new();

        public Command(string name, Action<string[], DevConsoleHost> handler)
        {
            Name = name;
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
            Scene scene,
            Action<string[], DevConsoleHost> handler,
            string signature,
            params string[] descriptionLines
        )
        {
            if (!Commands.TryGetValue(name, out var cmd))
            {
                cmd = new Command(name, handler);
                Commands[name] = cmd;
            }
            else if (cmd.Handler != handler)
            {
                Log.Error(
                    $"{LogPrefix} '{name}' re-registered with a different handler; later handler is ignored"
                );
            }
            cmd.HelpByScene[scene] = new HelpEntry(signature, descriptionLines);
        }

        public static void Execute(string line, DevConsoleHost host)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var name = tokens[0];
            var args = tokens.Skip(1).ToArray();

            if (!Commands.TryGetValue(name, out var cmd))
            {
                host.AppendLine($"unknown command: {name} (try {Cmd("help")})");
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
                Scene.Global,
                (_, host) => PrintHelp(host),
                "help",
                "list all commands"
            );

            Register(
                "clear",
                Scene.Global,
                (_, host) => host.Clear(),
                "clear",
                "clear the console"
            );

            BuiltinCommands.RegisterAll();
        }

        private static void PrintHelp(DevConsoleHost host)
        {
            PrintSection(host, "Global", Scene.Global);
            PrintSection(host, "Geoscape", Scene.Geoscape);
            PrintSection(host, "GroundCombat", Scene.GroundCombat);
        }

        private static void PrintSection(DevConsoleHost host, string label, Scene scene)
        {
            var entries = Commands
                .Values.Where(c => c.HelpByScene.ContainsKey(scene))
                .OrderBy(c => c.Name)
                .Select(c => (c.Name, Help: c.HelpByScene[scene]))
                .ToList();
            if (entries.Count == 0)
                return;

            host.AppendLine($"{label}:");
            foreach (var (_, help) in entries)
            {
                var desc = string.Join("; ", help.DescriptionLines);
                host.AppendLine($"  {Sig(help.Signature)} - {desc}");
            }
        }
    }
}
