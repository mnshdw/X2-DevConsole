using System.Collections.Generic;
using System.Linq;
using Xenonauts.Strategy.Components;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    public static class StrategyCommands
    {
        public static void ExecuteFunds(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 1, host, "funds <delta>"))
                return;
            if (!Args.TryParseInt(args[0], out var delta))
            {
                host.AppendLine($"funds: '{args[0]}' is not an integer");
                return;
            }
            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Geoscape");
                return;
            }
            if (!StrategyContext.TryGetPlayer(world, out var player))
            {
                host.AppendLine("could not find player entity");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            var newValue = StrategyContext.AddCash(player, delta);
            if (newValue == null)
            {
                host.AppendLine("player has no Cash component");
                return;
            }
            host.AppendLine($"funds: {(delta >= 0 ? "+" : "")}{delta} => ${newValue:N0}");
            Log.Info($"{LogPrefix} funds delta={delta} new={newValue}");
        }

        public static void ExecuteOp(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 1, host, "op <delta>"))
                return;
            if (!Args.TryParseInt(args[0], out var delta))
            {
                host.AppendLine($"op: '{args[0]}' is not an integer");
                return;
            }
            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Geoscape");
                return;
            }
            if (!StrategyContext.TryGetPlayer(world, out var player))
            {
                host.AppendLine("could not find player entity");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            var newValue = StrategyContext.AddOp(player, delta);
            if (newValue == null)
            {
                host.AppendLine("player has no OperationPoints component");
                return;
            }
            host.AppendLine($"op: {(delta >= 0 ? "+" : "")}{delta} => {newValue:N0}");
            Log.Info($"{LogPrefix} op delta={delta} new={newValue}");
        }

        public static void ExecuteEngineering(string[] args, DevConsoleHost host)
        {
            ExecuteProjectCommand(args, host, "engineering", ProjectType.Engineering());
        }

        public static void ExecuteResearch(string[] args, DevConsoleHost host)
        {
            ExecuteProjectCommand(args, host, "research", ProjectType.Research());
        }

        private static void ExecuteProjectCommand(
            string[] args,
            DevConsoleHost host,
            string commandName,
            ProjectType type
        )
        {
            if (args.Length == 0 || args[0] == "?" || args[0] == "help")
            {
                host.AppendLine($"usage: {Sig($"{commandName} complete [all|<name>]")}");
                host.AppendLine($"  complete         the currently in-progress {commandName}");
                host.AppendLine($"  complete all     every {commandName} project not yet finished");
                host.AppendLine($"  complete <name>  a specific project (substring match)");
                host.AppendLine($"  complete ?       list every {commandName} project by status");
                return;
            }
            var sub = args[0].ToLowerInvariant();
            var rest = args.Skip(1).ToArray();
            switch (sub)
            {
                case "complete":
                    ExecuteProjectComplete(rest, host, commandName, type);
                    return;
                default:
                    host.AppendLine(
                        $"{commandName}: unknown subcommand '{args[0]}' (try {Sig($"{commandName} ?")})"
                    );
                    return;
            }
        }

        private static void ExecuteProjectComplete(
            string[] args,
            DevConsoleHost host,
            string commandName,
            ProjectType type
        )
        {
            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Geoscape");
                return;
            }

            if (args.Length == 0)
            {
                BuiltinCommands.WarnOnce(host);
                var completed = StrategyContext.CompleteInProgressProjects(world, type);
                ReportProjectCompletion(host, commandName, "in-progress", completed);
                return;
            }

            if (args.Length == 1 && (args[0] == "?" || args[0].ToLowerInvariant() == "help"))
            {
                ListAllProjects(host, commandName, type, world);
                return;
            }

            if (args.Length == 1 && args[0].ToLowerInvariant() == "all")
            {
                var geoBase = StrategyContext.PickAnyAliveGeoBase(world);
                if (geoBase == null)
                {
                    host.AppendLine($"{commandName} complete all: no alive geo base to credit");
                    return;
                }
                BuiltinCommands.WarnOnce(host);
                var names = new List<string>();
                foreach (var p in StrategyContext.EnumerateUnfinishedProjects(world, type))
                    names.Add(StrategyContext.FinishProject(world, p, geoBase));
                ReportProjectCompletion(host, commandName, "unfinished", names);
                return;
            }

            var query = string.Join(" ", args);
            var matches = StrategyContext.FindProjectsByName(world, type, query);
            if (matches.Count == 0)
            {
                host.AppendLine(
                    $"{commandName} complete: no {commandName} project matching '{query}'"
                );
                return;
            }
            if (matches.Count > 1)
            {
                host.AppendLine(
                    $"{commandName} complete: '{query}' is ambiguous ({matches.Count} matches):"
                );
                foreach (var m in matches.Take(10))
                    host.AppendLine($"  {m.Name().value}");
                if (matches.Count > 10)
                    host.AppendLine($"  ... and {matches.Count - 10} more");
                return;
            }

            var match = matches[0];
            var baseForOne = StrategyContext.PickAnyAliveGeoBase(world);
            if (baseForOne == null)
            {
                host.AppendLine($"{commandName} complete: no alive geo base to credit");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            var name = StrategyContext.FinishProject(world, match, baseForOne);
            host.AppendLine($"{commandName} complete: finished {name}");
            Log.Info($"{LogPrefix} {commandName} complete: finished name={name}");
        }

        private static void ListAllProjects(
            DevConsoleHost host,
            string commandName,
            ProjectType type,
            Artitas.World world
        )
        {
            var listing = StrategyContext.ListProjects(world, type);
            host.AppendLine($"{commandName} projects ({listing.Total} total):");
            ListProjectGroup(host, "in progress", listing.InProgress);
            ListProjectGroup(host, "available", listing.Available);
            ListProjectGroup(host, "locked", listing.Locked);
            ListProjectGroup(host, "finished", listing.Finished);
        }

        private static void ListProjectGroup(DevConsoleHost host, string label, List<string> names)
        {
            if (names.Count == 0)
                return;
            host.AppendLine($"  {label} ({names.Count}):");
            host.AppendLine($"    {string.Join(", ", names)}");
        }

        private static void ReportProjectCompletion(
            DevConsoleHost host,
            string commandName,
            string scope,
            List<string> completed
        )
        {
            if (completed.Count == 0)
            {
                host.AppendLine($"{commandName} complete: no {scope} {commandName}");
                return;
            }
            var word = completed.Count == 1 ? "project" : "projects";
            host.AppendLine(
                $"{commandName} complete: finished {completed.Count} {word} ({string.Join(", ", completed)})"
            );
            Log.Info(
                $"{LogPrefix} {commandName} complete ({scope}): finished={completed.Count} names=[{string.Join(", ", completed)}]"
            );
        }

        private static readonly string[] AircraftSubcommands = { "add" };

        public static void ExecuteAircraft(string[] args, DevConsoleHost host)
        {
            if (args.Length == 0 || args[0] == "?" || args[0] == "help")
            {
                host.AppendLine($"usage: {Sig("aircraft <cmd> [args]")}");
                host.AppendLine($"  subcommands: {string.Join(", ", AircraftSubcommands)}");
                return;
            }
            var sub = args[0].ToLowerInvariant();
            var rest = args.Skip(1).ToArray();
            switch (sub)
            {
                case "add":
                    ExecuteAircraftAdd(rest, host);
                    return;
                default:
                    host.AppendLine(
                        $"aircraft: unknown subcommand '{args[0]}' (try {Sig("aircraft ?")})"
                    );
                    return;
            }
        }

        private static void ExecuteAircraftAdd(string[] args, DevConsoleHost host)
        {
            if (args.Length > 2)
            {
                host.AppendLine($"usage: {Sig("aircraft add [type [base]]")}");
                return;
            }
            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Geoscape");
                return;
            }

            if (args.Length == 0 || args[0] == "?" || args[0] == "help")
            {
                var types = StrategyContext.ListAircraftTypeNames(world, out var status);
                if (types.Count == 0)
                {
                    host.AppendLine($"no aircraft templates found: {status}");
                    return;
                }
                host.AppendLine($"aircraft types ({types.Count}):");
                host.AppendLine("  " + string.Join(", ", types));
                host.AppendLine($"usage: {Sig("aircraft add <type> [base]")}");
                return;
            }

            var typeQuery = args[0];
            var baseQuery = args.Length >= 2 ? args[1] : null;

            BuiltinCommands.WarnOnce(host);
            if (
                !StrategyContext.TrySpawnXenonautAircraft(
                    world,
                    typeQuery,
                    baseQuery,
                    out var spawned,
                    out var matchedType,
                    out var matchedBase,
                    out var reason
                )
            )
            {
                host.AppendLine($"aircraft add ({typeQuery}) failed: {reason}");
                return;
            }
            var label = matchedType ?? typeQuery;
            var baseLabel = matchedBase ?? "?";
            var name = spawned!.HasName() ? spawned.Name().value : spawned.ToString();
            host.AppendLine($"added aircraft {label} at base {baseLabel}: {name}");
            Log.Info($"{LogPrefix} aircraft add: type={label} base={baseLabel} entity={spawned}");
        }
    }
}
