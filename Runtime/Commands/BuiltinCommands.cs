using System.Collections.Generic;
using System.Linq;
using Xenonauts.Common.Stats;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    public static class BuiltinCommands
    {
        private static bool _warnedThisSession;
        private static bool _xrayOn;

        public static void RegisterAll()
        {
            CommandRegistry.Register(
                "funds",
                Scene.Geoscape,
                ExecuteFunds,
                "funds <delta>",
                "add or remove Cash (eg. funds 5000, funds -1000)"
            );

            CommandRegistry.Register(
                "op",
                Scene.Geoscape,
                ExecuteOp,
                "op <delta>",
                "add or remove Operation Points"
            );

            CommandRegistry.Register(
                "stat",
                Scene.Geoscape,
                StatCommand.Execute,
                "stat <stat|all> <delta> <name>",
                "adjust a soldier or aircraft's stats (eg. stat strength 5 jones, stat fuel 50 angel)",
                "stat ? lists stats per kind"
            );

            CommandRegistry.Register(
                "aircraft",
                Scene.Geoscape,
                ExecuteAircraft,
                "aircraft <cmd> [args]",
                "aircraft add <type> [base] - spawn an aircraft into an empty hangar",
                "aircraft add ? lists aircraft types"
            );

            CommandRegistry.Register(
                "restore",
                Scene.Geoscape,
                ExecuteRestore,
                "restore <stat|all> <name>",
                "restore stats by name",
                "soldier: health, timeunits",
                "aircraft: health, fuel, armor",
                "restore ? lists stats"
            );

            CommandRegistry.Register(
                "xray",
                Scene.GroundCombat,
                ExecuteXray,
                "xray",
                "toggle X-ray vision on enemy silhouettes (does not lift fog of war)"
            );

            CommandRegistry.Register(
                "kill",
                Scene.GroundCombat,
                ExecuteKill,
                "kill",
                "kill the combatant under the mouse cursor"
            );

            CommandRegistry.Register(
                "spawn",
                Scene.GroundCombat,
                ExecuteSpawn,
                "spawn [species [rank]]",
                "spawn an alien on the tile under the mouse cursor",
                "with no arg, copies from an alien on the map",
                "spawn ? lists species",
                "spawn <species> ? lists ranks"
            );

            CommandRegistry.Register(
                "restore",
                Scene.GroundCombat,
                ExecuteRestore,
                "restore <stat|all>",
                "restore stats for the combatant under the mouse cursor",
                "restore ? lists stats"
            );
        }

        private static void ExecuteFunds(string[] args, DevConsoleHost host)
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
            WarnOnce(host);
            var newValue = StrategyContext.AddCash(player, delta);
            if (newValue == null)
            {
                host.AppendLine("player has no Cash component");
                return;
            }
            host.AppendLine($"funds: {(delta >= 0 ? "+" : "")}{delta} => ${newValue:N0}");
            Log.Info($"{LogPrefix} funds delta={delta} new={newValue}");
        }

        private static void ExecuteOp(string[] args, DevConsoleHost host)
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
            WarnOnce(host);
            var newValue = StrategyContext.AddOp(player, delta);
            if (newValue == null)
            {
                host.AppendLine("player has no OperationPoints component");
                return;
            }
            host.AppendLine($"op: {(delta >= 0 ? "+" : "")}{delta} => {newValue:N0}");
            Log.Info($"{LogPrefix} op delta={delta} new={newValue}");
        }

        private static void ExecuteXray(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 0, host, "xray"))
                return;
            if (!GroundCombatContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in GroundCombat");
                return;
            }
            var nextOn = !_xrayOn;
            if (nextOn)
                WarnOnce(host);
            if (!GroundCombatContext.SetXray(world, nextOn))
            {
                host.AppendLine("could not toggle xray (visibility system missing)");
                return;
            }
            _xrayOn = nextOn;
            host.AppendLine($"xray: {(_xrayOn ? "on" : "off")}");
            Log.Info($"{LogPrefix} xray: {(_xrayOn ? "on" : "off")}");
        }

        private static void ExecuteKill(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 0, host, "kill"))
                return;
            if (!GroundCombatContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in GroundCombat");
                return;
            }
            if (!GroundCombatContext.TryGetCursorPick(world, out var target, out _))
            {
                host.AppendLine("no cursor pick yet (move the mouse over a tile first)");
                return;
            }
            if (target == null)
            {
                host.AppendLine("no entity under cursor");
                return;
            }
            WarnOnce(host);
            if (!GroundCombatContext.TryKillCombatant(world, target, out var reason))
            {
                host.AppendLine($"kill refused: {reason}");
                return;
            }
            host.AppendLine($"killed: {target}");
            Log.Info($"{LogPrefix} kill: target={target}");
        }

        private static void ExecuteSpawn(string[] args, DevConsoleHost host)
        {
            if (args.Length > 2)
            {
                host.AppendLine($"usage: {Sig("spawn [species [rank]]")}");
                return;
            }
            var speciesName = args.Length >= 1 ? args[0] : null;
            var rankName = args.Length >= 2 ? args[1] : null;

            if (speciesName == "?" || speciesName == "help")
            {
                var species = LoadoutRegistry.Species.OrderBy(s => s).ToList();
                if (species.Count == 0)
                {
                    host.AppendLine(
                        "loadout registry empty (content manager not ready, or no GroundCombat world loaded yet)"
                    );
                }
                else
                {
                    host.AppendLine($"species with shipped loadouts ({species.Count}):");
                    host.AppendLine("  " + string.Join(", ", species));
                    host.AppendLine($"for ranks: {Sig("spawn <species> ?")}");
                    host.AppendLine(
                        "with no arg, spawn copies the species of any alien already on the map"
                    );
                }
                return;
            }

            if (rankName == "?" || rankName == "help")
            {
                var ranks = LoadoutRegistry
                    .RanksFor(speciesName!.ToLowerInvariant())
                    .OrderBy(r => r)
                    .ToList();
                if (ranks.Count == 0)
                {
                    host.AppendLine(
                        $"no shipped loadouts for species '{speciesName}' (try {Sig("spawn ?")} for the list)"
                    );
                }
                else
                {
                    host.AppendLine($"ranks for {speciesName} ({ranks.Count}):");
                    host.AppendLine("  " + string.Join(", ", ranks));
                }
                return;
            }

            if (!GroundCombatContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in GroundCombat");
                return;
            }
            if (!GroundCombatContext.TryGetCursorPick(world, out _, out var address))
            {
                host.AppendLine("no cursor pick yet (move the mouse over a tile first)");
                return;
            }
            WarnOnce(host);
            if (
                !GroundCombatContext.TrySpawnHostileAt(
                    world,
                    address,
                    speciesName,
                    rankName,
                    out var spawned,
                    out var reason
                )
            )
            {
                var label = FormatLabel(speciesName, rankName);
                host.AppendLine($"spawn ({label}) failed: {reason}");
                return;
            }
            var labelOk = FormatLabel(speciesName, rankName);
            host.AppendLine($"spawned {labelOk}: {spawned} at ({address.i},{address.j})");
            Log.Info(
                $"{LogPrefix} spawn: {labelOk} spawned={spawned} at=({address.i},{address.j})"
            );
        }

        private static readonly string[] AircraftSubcommands = { "add" };

        private static void ExecuteAircraft(string[] args, DevConsoleHost host)
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

            WarnOnce(host);
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

        private static string FormatLabel(string? species, string? rank)
        {
            if (species == null)
                return "auto";
            return rank == null ? species : $"{species}/{rank}";
        }

        // Restore vocabulary by entity kind. Components touched are RangeComponents;
        // MaxStat raises Value to Maximum, except for Stun (cleared via ZeroStat).
        private static readonly string[] SoldierRestorables = { "health", "timeunits" };
        private static readonly string[] AircraftRestorables = { "health", "fuel", "armor" };

        private static void ExecuteRestore(string[] args, DevConsoleHost host)
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

        private static void ApplyRestoreSoldier(
            DevConsoleHost host,
            Artitas.Entity target,
            string statArg
        )
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

        private static void ApplyRestoreAircraft(
            DevConsoleHost host,
            Artitas.Entity target,
            string statArg
        )
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
            Artitas.Entity target,
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
            WarnOnce(host);
            var msg = $"restore ({name}): {string.Join(", ", applied)}";
            if (noop.Count > 0)
                msg += $" (already at max: {string.Join(", ", noop)})";
            host.AppendLine(msg);
            Log.Info(
                $"{LogPrefix} restore: target={target} applied=[{string.Join(",", applied)}] noop=[{string.Join(",", noop)}]"
            );
        }

        public static void WarnOnce(DevConsoleHost host)
        {
            if (_warnedThisSession)
                return;
            _warnedThisSession = true;
            const string msg =
                "warning: state-mutating commands can corrupt save state. Save before using.";
            host.AppendLine($"<color=#ffd166>{msg}</color>");
            Log.Info($"{LogPrefix} {msg}");
        }
    }
}
