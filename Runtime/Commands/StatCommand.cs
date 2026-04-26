using System.Collections.Generic;
using System.Linq;
using DevConsole.Runtime.Stats;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    // stat <kind> <stat|all> <delta> [name]
    //   kinds: soldier (aircraft, vehicle to follow)
    //   stat:  one of the names in the kind's stat table, or 'all' to apply the
    //          delta to every stat flagged IncludedInAll
    //   delta: signed integer (use '5' or '-5'; '+5' is rejected)
    //   name:  required for v1; soldier name (case-insensitive)
    public static class StatCommand
    {
        private const string Usage = "stat <kind> <stat|all> <delta> [name]";

        private static readonly string[] Kinds = { "soldier" };

        public static void Execute(string[] args, DevConsoleHost host)
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                host.AppendLine($"usage: {Usage}");
                host.AppendLine("kinds: " + string.Join(", ", Kinds));
                host.AppendLine("for stats of a kind: stat <kind> ?");
                return;
            }

            var kind = args[0].ToLowerInvariant();
            switch (kind)
            {
                case "soldier":
                    ExecuteSoldier(args, host);
                    return;
                case "aircraft":
                case "vehicle":
                    host.AppendLine($"stat: kind '{kind}' not implemented yet");
                    return;
                default:
                    host.AppendLine($"stat: unknown kind '{args[0]}' (try 'stat ?')");
                    return;
            }
        }

        private static void ExecuteSoldier(string[] args, DevConsoleHost host)
        {
            // args[0] = "soldier"
            if (args.Length >= 2 && IsHelp(args[1]))
            {
                ListSoldierStats(host);
                return;
            }

            if (args.Length < 4)
            {
                host.AppendLine("usage: stat soldier <stat|all> <delta> <name>");
                host.AppendLine(
                    "name may contain spaces and matches case-insensitively (substring ok)"
                );
                host.AppendLine("for the stat list: stat soldier ?");
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

            var matches = StrategyContext.FindActorsByName(world, nameArg);
            if (matches.Count == 0)
            {
                host.AppendLine($"stat soldier: no soldier matching '{nameArg}'");
                return;
            }
            if (matches.Count > 1)
            {
                host.AppendLine(
                    $"stat soldier: '{nameArg}' is ambiguous ({matches.Count} matches):"
                );
                foreach (var m in matches.Take(10))
                    host.AppendLine($"  {m.Name().value}");
                if (matches.Count > 10)
                    host.AppendLine($"  ... and {matches.Count - 10} more");
                return;
            }
            var actor = matches[0];

            List<StatEntry> targets;
            if (string.Equals(statArg, "all", System.StringComparison.OrdinalIgnoreCase))
            {
                targets = SoldierStatTable.All.Where(e => e.IncludedInAll).ToList();
            }
            else if (SoldierStatTable.TryFind(statArg, out var entry))
            {
                targets = new List<StatEntry> { entry };
            }
            else
            {
                host.AppendLine($"stat soldier: unknown stat '{statArg}' (try 'stat soldier ?')");
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
                    $"stat soldier ({displayName}): no stats applied (missing components: {string.Join(", ", skipped)})"
                );
                return;
            }

            var sign = delta >= 0 ? "+" : "";
            host.AppendLine(
                $"stat soldier ({displayName}) {sign}{delta}: {string.Join(", ", applied)}"
            );
            if (skipped.Count > 0)
                host.AppendLine($"  skipped (no component): {string.Join(", ", skipped)}");
            Log.Info(
                $"{LogPrefix} stat soldier name={displayName} delta={delta} applied=[{string.Join(",", applied)}]"
            );
        }

        private static void ListSoldierStats(DevConsoleHost host)
        {
            var inAll = SoldierStatTable
                .All.Where(e => e.IncludedInAll)
                .Select(e => e.Name)
                .ToList();
            var notInAll = SoldierStatTable
                .All.Where(e => !e.IncludedInAll)
                .Select(e => e.Name)
                .ToList();
            host.AppendLine("soldier stats:");
            host.AppendLine("  in 'all': " + string.Join(", ", inAll));
            host.AppendLine("  excluded from 'all': " + string.Join(", ", notInAll));
        }

        private static bool IsHelp(string s) => s == "?" || s == "help";
    }
}
