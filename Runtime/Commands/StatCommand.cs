using System;
using System.Collections.Generic;
using System.Linq;
using Artitas;
using DevConsole.Runtime.Stats;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    // stat <kind> <stat|all> <delta> [name]
    //   kinds: soldier, aircraft
    //   stat:  one of the names in the kind's stat table, or 'all' to apply the
    //          delta to every stat flagged IncludedInAll
    //   delta: signed integer (use '5' or '-5'; '+5' is rejected)
    //   name:  case-insensitive substring match
    public static class StatCommand
    {
        private const string Usage = "stat <kind> <stat|all> <delta> [name]";

        private static readonly string[] Kinds = { "soldier", "aircraft" };

        public static void Execute(string[] args, DevConsoleHost host)
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                host.AppendLine($"usage: {Cmd(Usage)}");
                host.AppendLine("kinds: " + string.Join(", ", Kinds));
                host.AppendLine($"for stats of a kind: {Cmd("stat <kind> ?")}");
                return;
            }

            var kind = args[0].ToLowerInvariant();
            switch (kind)
            {
                case "soldier":
                    ExecuteForKind(
                        "soldier",
                        args,
                        host,
                        SoldierStatTable.All,
                        SoldierStatTable.TryFind,
                        StrategyContext.FindActorsByName
                    );
                    return;
                case "aircraft":
                    ExecuteForKind(
                        "aircraft",
                        args,
                        host,
                        AircraftStatTable.All,
                        AircraftStatTable.TryFind,
                        StrategyContext.FindAircraftByName
                    );
                    return;
                default:
                    host.AppendLine($"stat: unknown kind '{args[0]}' (try {Cmd("stat ?")})");
                    return;
            }
        }

        private delegate bool TryFindStat(string name, out StatEntry entry);

        private delegate List<Entity> FindByName(World world, string query);

        private static void ExecuteForKind(
            string kind,
            string[] args,
            DevConsoleHost host,
            IReadOnlyList<StatEntry> table,
            TryFindStat tryFind,
            FindByName findByName
        )
        {
            // args[0] = kind
            if (args.Length >= 2 && IsHelp(args[1]))
            {
                ListStats(host, kind, table);
                return;
            }

            if (args.Length < 4)
            {
                host.AppendLine($"usage: {Cmd($"stat {kind} <stat|all> <delta> <name>")}");
                host.AppendLine(
                    "name may contain spaces and matches case-insensitively (substring ok)"
                );
                host.AppendLine($"for the stat list: {Cmd($"stat {kind} ?")}");
                return;
            }

            var statArg = args[1];
            var deltaArg = args[2];
            var nameArg = string.Join(" ", args.Skip(3));

            if (!Args.TryParseInt(deltaArg, out var delta))
            {
                host.AppendLine($"stat: '{deltaArg}' is not an integer (use 5 or -5, not +5)");
                return;
            }

            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Strategy");
                return;
            }

            var matches = findByName(world, nameArg);
            if (matches.Count == 0)
            {
                host.AppendLine($"stat {kind}: no {kind} matching '{nameArg}'");
                return;
            }
            if (matches.Count > 1)
            {
                host.AppendLine(
                    $"stat {kind}: '{nameArg}' is ambiguous ({matches.Count} matches):"
                );
                foreach (var m in matches.Take(10))
                    host.AppendLine($"  {m.Name().value}");
                if (matches.Count > 10)
                    host.AppendLine($"  ... and {matches.Count - 10} more");
                return;
            }
            var actor = matches[0];

            List<StatEntry> targets;
            if (string.Equals(statArg, "all", StringComparison.OrdinalIgnoreCase))
            {
                targets = table.Where(e => e.IncludedInAll).ToList();
            }
            else if (tryFind(statArg, out var entry))
            {
                targets = new List<StatEntry> { entry };
            }
            else
            {
                host.AppendLine(
                    $"stat {kind}: unknown stat '{statArg}' (try {Cmd($"stat {kind} ?")})"
                );
                return;
            }

            BuiltinCommands.WarnOnce(host);

            var displayName = actor.HasName() ? actor.Name().value : nameArg;
            var applied = new List<string>();
            var skipped = new List<string>();
            foreach (var entry in targets)
            {
                var primaryHit = false;
                foreach (var change in entry.Changes)
                {
                    var ok = StrategyContext.AddStat(
                        actor,
                        change.ComponentType,
                        delta,
                        change.MaxOnly
                    );
                    if (ok && ReferenceEquals(change.ComponentType, entry.Changes[0].ComponentType))
                        primaryHit = true;
                }
                if (!primaryHit)
                {
                    skipped.Add(entry.Name);
                    continue;
                }
                var shown = StrategyContext.ReadStat(actor, entry.ReadType, entry.ReadMaximum);
                applied.Add(shown.HasValue ? $"{entry.Name}={shown:0}" : $"{entry.Name}=?");
            }

            if (applied.Count == 0)
            {
                host.AppendLine(
                    $"stat {kind} ({displayName}): no stats applied (missing components: {string.Join(", ", skipped)})"
                );
                return;
            }

            var sign = delta >= 0 ? "+" : "";
            host.AppendLine(
                $"stat {kind} ({displayName}) {sign}{delta}: {string.Join(", ", applied)}"
            );
            if (skipped.Count > 0)
                host.AppendLine($"  skipped (no component): {string.Join(", ", skipped)}");
            Log.Info(
                $"{LogPrefix} stat {kind} name={displayName} delta={delta} applied=[{string.Join(",", applied)}]"
            );
        }

        private static void ListStats(
            DevConsoleHost host,
            string kind,
            IReadOnlyList<StatEntry> table
        )
        {
            var inAll = table.Where(e => e.IncludedInAll).Select(e => e.Name).ToList();
            var notInAll = table.Where(e => !e.IncludedInAll).Select(e => e.Name).ToList();
            host.AppendLine($"{kind} stats:");
            host.AppendLine("  in 'all': " + string.Join(", ", inAll));
            if (notInAll.Count > 0)
                host.AppendLine("  excluded from 'all': " + string.Join(", ", notInAll));
        }

        private static bool IsHelp(string s) => s == "?" || s == "help";
    }
}
