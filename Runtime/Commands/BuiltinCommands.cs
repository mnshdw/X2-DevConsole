using System.Linq;
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
                "funds <delta> - adjust Cash on Geoscape (e.g. funds 5000, funds -1000)",
                ExecuteFunds
            );

            CommandRegistry.Register(
                "op",
                "op <delta> - adjust Operation Points on Geoscape (e.g. op 5)",
                ExecuteOp
            );

            CommandRegistry.Register(
                "xray",
                "xray - toggle X-ray vision on enemy silhouettes (does not lift fog of war)",
                ExecuteXray
            );

            CommandRegistry.Register(
                "kill",
                "kill - kill the combatant under the mouse cursor",
                ExecuteKill
            );

            CommandRegistry.Register(
                "spawn",
                "spawn [species [rank]] - spawn an alien at the mouse cursor. 'spawn ?' lists species; 'spawn <species> ?' lists ranks.",
                ExecuteSpawn
            );

            CommandRegistry.Register(
                "stat",
                "stat <kind> <stat|all> <delta> [name] - adjust unit stats on Geoscape. 'stat ?' lists kinds; 'stat <kind> ?' lists stats.",
                StatCommand.Execute
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
                host.AppendLine("not in Strategy");
                return;
            }
            if (!StrategyContext.TryGetPlayer(world, out var player))
            {
                host.AppendLine("could not find XENONAUT player entity");
                return;
            }
            WarnOnce(host);
            var newValue = StrategyContext.AddCash(player, delta);
            if (newValue == null)
            {
                host.AppendLine("XENONAUT player has no Cash component");
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
                host.AppendLine("not in Strategy");
                return;
            }
            if (!StrategyContext.TryGetPlayer(world, out var player))
            {
                host.AppendLine("could not find XENONAUT player entity");
                return;
            }
            WarnOnce(host);
            var newValue = StrategyContext.AddOp(player, delta);
            if (newValue == null)
            {
                host.AppendLine("XENONAUT player has no OperationPoints component");
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
                host.AppendLine("usage: spawn [species [rank]]");
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
                    host.AppendLine("for ranks: spawn <species> ?");
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
                        $"no shipped loadouts for species '{speciesName}' (try 'spawn ?' for the list)"
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

        private static string FormatLabel(string? species, string? rank)
        {
            if (species == null)
                return "auto";
            return rank == null ? species : $"{species}/{rank}";
        }

        public static void WarnOnce(DevConsoleHost host)
        {
            if (_warnedThisSession)
                return;
            _warnedThisSession = true;
            const string msg =
                "warning: state-mutating commands can corrupt save state. Save before using.";
            host.AppendLine(msg);
            Log.Info($"{LogPrefix} {msg}");
        }
    }
}
