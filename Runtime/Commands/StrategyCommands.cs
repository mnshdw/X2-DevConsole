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

        private static readonly string[] ResearchSubcommands = { "complete" };
        private static readonly string[] EngineeringSubcommands = { "complete" };

        public static void ExecuteResearch(string[] args, DevConsoleHost host)
        {
            ExecuteProjectCommand(
                args,
                host,
                "research",
                ResearchSubcommands,
                ProjectType.Research()
            );
        }

        public static void ExecuteEngineering(string[] args, DevConsoleHost host)
        {
            ExecuteProjectCommand(
                args,
                host,
                "engineering",
                EngineeringSubcommands,
                ProjectType.Engineering()
            );
        }

        private static void ExecuteProjectCommand(
            string[] args,
            DevConsoleHost host,
            string commandName,
            string[] subcommands,
            ProjectType type
        )
        {
            if (args.Length == 0 || args[0] == "?" || args[0] == "help")
            {
                host.AppendLine($"usage: {Sig($"{commandName} <cmd>")}");
                host.AppendLine($"  subcommands: {string.Join(", ", subcommands)}");
                return;
            }
            var sub = args[0].ToLowerInvariant();
            if (args.Length > 1)
            {
                host.AppendLine($"usage: {Sig($"{commandName} {sub}")}");
                return;
            }
            switch (sub)
            {
                case "complete":
                    ExecuteProjectComplete(host, commandName, type);
                    return;
                default:
                    host.AppendLine(
                        $"{commandName}: unknown subcommand '{args[0]}' (try {Sig($"{commandName} ?")})"
                    );
                    return;
            }
        }

        private static void ExecuteProjectComplete(
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
            BuiltinCommands.WarnOnce(host);
            var completed = StrategyContext.CompleteInProgressProjects(world, type);
            if (completed.Count == 0)
            {
                host.AppendLine($"{commandName} complete: no in-progress {commandName}");
                return;
            }
            var word = completed.Count == 1 ? "project" : "projects";
            host.AppendLine(
                $"{commandName} complete: finished {completed.Count} {word} ({string.Join(", ", completed)})"
            );
            Log.Info(
                $"{LogPrefix} {commandName} complete: finished={completed.Count} names=[{string.Join(", ", completed)}]"
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
