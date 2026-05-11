using System.Collections.Generic;
using System.Linq;
using Artitas;
using Xenonauts.Common.Stats;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    // Shared between Geoscape and GroundCombat: dispatches by world.
    // GroundCombat targets the unit under the cursor (no name).
    // Geoscape resolves a soldier or aircraft by name (substring match).
    public static class RestoreCommand
    {
        // Restore vocabulary by entity kind. Components touched are RangeComponents;
        // MaxStat raises Value to Maximum, except for Stun (cleared via ZeroStat).
        private static readonly string[] SoldierRestorables = { "health", "timeunits" };
        private static readonly string[] AircraftRestorables = { "health", "fuel", "armor" };

        public static void Execute(string[] args, DevConsoleHost host)
        {
            if (args.Length == 0 || args[0] == "?" || args[0] == "help")
            {
                host.AppendLine($"usage: {Sig("restore <stat|all> [name]")}");
                host.AppendLine($"  soldier: {string.Join(", ", SoldierRestorables)}");
                host.AppendLine($"  aircraft: {string.Join(", ", AircraftRestorables)}");
                host.AppendLine("GroundCombat: targets the unit under cursor (no name)");
                host.AppendLine("Geoscape: matches a soldier or aircraft by name");
                return;
            }
            var statArg = args[0].ToLowerInvariant();
            if (
                statArg != "all"
                && !SoldierRestorables.Contains(statArg)
                && !AircraftRestorables.Contains(statArg)
            )
            {
                host.AppendLine($"restore: unknown stat '{args[0]}' (try {Sig("restore ?")})");
                return;
            }

            if (GroundCombatContext.TryGetWorld(out var gcWorld))
            {
                if (args.Length > 1)
                {
                    host.AppendLine(
                        "restore: in GroundCombat the target is the unit under cursor (no name)"
                    );
                    return;
                }
                if (!GroundCombatContext.TryGetCursorPick(gcWorld, out var target, out _))
                {
                    host.AppendLine("no cursor pick yet (move the mouse over a tile first)");
                    return;
                }
                if (target == null)
                {
                    host.AppendLine("no entity under cursor");
                    return;
                }
                ApplyRestoreSoldier(host, target, statArg);
                return;
            }

            if (StrategyContext.TryGetWorld(out var stratWorld))
            {
                if (args.Length < 2)
                {
                    host.AppendLine($"usage: {Sig("restore <stat|all> <name>")}");
                    return;
                }
                var nameArg = string.Join(" ", args.Skip(1));
                var matches = StrategyContext.FindNamed(stratWorld, nameArg);
                if (matches.Total == 0)
                {
                    host.AppendLine($"restore: no soldier or aircraft matching '{nameArg}'");
                    return;
                }
                if (matches.Total > 1)
                {
                    host.AppendLine(
                        $"restore: '{nameArg}' is ambiguous ({matches.Total} matches):"
                    );
                    foreach (var m in matches.Actors.Concat(matches.Aircraft).Take(10))
                        host.AppendLine($"  {m.Name().value}");
                    if (matches.Total > 10)
                        host.AppendLine($"  ... and {matches.Total - 10} more");
                    return;
                }
                if (matches.Actors.Count == 1)
                    ApplyRestoreSoldier(host, matches.Actors[0], statArg);
                else
                    ApplyRestoreAircraft(host, matches.Aircraft[0], statArg);
                return;
            }

            host.AppendLine("not in Geoscape or GroundCombat");
        }

        private static void ApplyRestoreSoldier(DevConsoleHost host, Entity target, string statArg)
        {
            var applied = new List<string>();
            var noop = new List<string>();
            if (statArg == "all" || statArg == "health")
            {
                var hpExists = StrategyContext.MaxStat(
                    target,
                    typeof(HitPoints),
                    out var hpChanged
                );
                if (hpExists)
                {
                    StrategyContext.ZeroStat(target, typeof(Stun), out var stunChanged);
                    if (hpChanged || stunChanged)
                        applied.Add("health");
                    else
                        noop.Add("health");
                }
            }
            if (statArg == "all" || statArg == "timeunits")
            {
                if (StrategyContext.MaxStat(target, typeof(TimeUnits), out var tuChanged))
                    (tuChanged ? applied : noop).Add("timeunits");
            }
            ReportRestore(host, target, applied, noop, statArg);
        }

        private static void ApplyRestoreAircraft(DevConsoleHost host, Entity target, string statArg)
        {
            var applied = new List<string>();
            var noop = new List<string>();
            if (statArg == "all" || statArg == "health")
            {
                if (StrategyContext.MaxStat(target, typeof(HitPoints), out var hpChanged))
                    (hpChanged ? applied : noop).Add("health");
            }
            if (statArg == "all" || statArg == "fuel")
            {
                if (
                    StrategyContext.MaxStat(
                        target,
                        typeof(Xenonauts.Strategy.Components.Fuel),
                        out var fuelChanged
                    )
                )
                    (fuelChanged ? applied : noop).Add("fuel");
            }
            if (statArg == "all" || statArg == "armor")
            {
                if (
                    StrategyContext.MaxStat(
                        target,
                        typeof(Xenonauts.Strategy.Components.AirCombatArmorComponent),
                        out var armorChanged
                    )
                )
                    (armorChanged ? applied : noop).Add("armor");
            }
            ReportRestore(host, target, applied, noop, statArg);
        }

        private static void ReportRestore(
            DevConsoleHost host,
            Entity target,
            List<string> applied,
            List<string> noop,
            string statArg
        )
        {
            var name = target.HasName() ? target.Name().value : target.ToString();
            if (applied.Count == 0 && noop.Count == 0)
            {
                host.AppendLine(
                    statArg == "all"
                        ? $"restore ({name}): no restorable components"
                        : $"restore ({name}): '{statArg}' not applicable to this target"
                );
                return;
            }
            if (applied.Count == 0)
            {
                host.AppendLine($"restore ({name}): already at max ({string.Join(", ", noop)})");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            var msg = $"restore ({name}): {string.Join(", ", applied)}";
            if (noop.Count > 0)
                msg += $" (already at max: {string.Join(", ", noop)})";
            host.AppendLine(msg);
            Log.Info(
                $"{LogPrefix} restore: target={target} applied=[{string.Join(",", applied)}] noop=[{string.Join(",", noop)}]"
            );
        }
    }
}
