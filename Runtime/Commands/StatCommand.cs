using System;
using System.Collections.Generic;
using System.Linq;
using Artitas;
using DevConsole.Runtime.Stats;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    // stat <stat|all> <delta> <name>
    //   stat:  one of the names in the target stat table, or 'all' to apply the
    //          delta to every stat flagged IncludedInAll
    //   delta: signed integer (use '5' or '-5'; '+5' is rejected)
    //   name:  case-insensitive substring match
    public static class StatCommand
    {
        private const string Usage = "stat <stat|all> <delta> <name>";

        public static void Execute(string[] args, DevConsoleHost host)
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                ListStats(host);
                return;
            }

            if (args.Length < 3)
            {
                host.AppendLine($"usage: {Sig(Usage)}");
                host.AppendLine($"for the stat list: {Cmd("stat")} ?");
                return;
            }

            var statArg = args[0];
            var deltaArg = args[1];
            var nameArg = string.Join(" ", args.Skip(2));

            if (!Args.TryParseInt(deltaArg, out var delta))
            {
                host.AppendLine($"stat: '{deltaArg}' is not an integer (use 5 or -5, not +5)");
                return;
            }

            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Geoscape");
                return;
            }

            var matches = StrategyContext.FindNamed(world, nameArg);
            if (matches.Total == 0)
            {
                host.AppendLine($"stat: no match for '{nameArg}'");
                return;
            }
            if (matches.Total > 1)
            {
                host.AppendLine($"stat: '{nameArg}' is ambiguous ({matches.Total} matches):");
                foreach (var m in matches.Actors.Concat(matches.Aircraft).Take(10))
                    host.AppendLine($"  {m.Name().value}");
                if (matches.Total > 10)
                    host.AppendLine($"  ... and {matches.Total - 10} more");
                return;
            }

            Entity target;
            string kind;
            IReadOnlyList<StatEntry> table;
            TryFindStat tryFind;
            if (matches.Actors.Count == 1)
            {
                target = matches.Actors[0];
                kind = "soldier";
                table = SoldierStatTable.All;
                tryFind = SoldierStatTable.TryFind;
            }
            else
            {
                target = matches.Aircraft[0];
                kind = "aircraft";
                table = AircraftStatTable.All;
                tryFind = AircraftStatTable.TryFind;
            }

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
                host.AppendLine($"stat: '{statArg}' is not a {kind} stat (try {Cmd("stat")} ?)");
                return;
            }

            BuiltinCommands.WarnOnce(host);

            var displayName = target.HasName() ? target.Name().value : nameArg;
            var applied = new List<string>();
            var skipped = new List<string>();
            foreach (var entry in targets)
            {
                var primaryHit = false;
                foreach (var change in entry.Changes)
                {
                    var ok = StrategyContext.AddStat(
                        target,
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
                var shown = StrategyContext.ReadStat(target, entry.ReadType, entry.ReadMaximum);
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

        private delegate bool TryFindStat(string name, out StatEntry entry);

        private static void ListStats(DevConsoleHost host)
        {
            host.AppendLine($"usage: {Sig(Usage)}");
            DumpKind(host, "soldier", SoldierStatTable.All);
            DumpKind(host, "aircraft", AircraftStatTable.All);
        }

        private static void DumpKind(
            DevConsoleHost host,
            string label,
            IReadOnlyList<StatEntry> table
        )
        {
            var inAll = table.Where(e => e.IncludedInAll).Select(e => e.Name).ToList();
            var notInAll = table.Where(e => !e.IncludedInAll).Select(e => e.Name).ToList();
            host.AppendLine($"{label}:");
            host.AppendLine("  in 'all': " + string.Join(", ", inAll));
            if (notInAll.Count > 0)
                host.AppendLine("  excluded from 'all': " + string.Join(", ", notInAll));
        }

        private static bool IsHelp(string s) => s == "?" || s == "help";
    }
}
