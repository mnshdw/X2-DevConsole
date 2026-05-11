using System.Linq;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    public static class GroundCombatCommands
    {
        private static bool _xrayOn;

        public static void ExecuteStun(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 0, host, "stun"))
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
            BuiltinCommands.WarnOnce(host);
            if (!GroundCombatContext.TryStunCombatant(target, out var reason))
            {
                host.AppendLine($"stun refused: {reason}");
                return;
            }
            host.AppendLine($"stunned: {target}");
            Log.Info($"{LogPrefix} stun: target={target}");
        }

        public static void ExecuteStunAll(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 0, host, "stunall"))
                return;
            if (!GroundCombatContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in GroundCombat");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            var n = GroundCombatContext.StunAllAliens(world);
            host.AppendLine($"stunned {n} alien combatant(s)");
            Log.Info($"{LogPrefix} stunall: count={n}");
        }

        public static void ExecuteKill(string[] args, DevConsoleHost host)
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
            BuiltinCommands.WarnOnce(host);
            if (!GroundCombatContext.TryKillCombatant(world, target, out var reason))
            {
                host.AppendLine($"kill refused: {reason}");
                return;
            }
            host.AppendLine($"killed: {target}");
            Log.Info($"{LogPrefix} kill: target={target}");
        }

        public static void ExecuteKillAll(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 0, host, "killall"))
                return;
            if (!GroundCombatContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in GroundCombat");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            var n = GroundCombatContext.KillAllAliens(world);
            host.AppendLine($"killed {n} alien combatant(s)");
            Log.Info($"{LogPrefix} killall: count={n}");
        }

        public static void ExecuteReload(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 0, host, "reload"))
                return;
            if (!GroundCombatContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in GroundCombat");
                return;
            }
            if (!GroundCombatContext.TryGetSelectedUnit(world, out var unit))
            {
                host.AppendLine("no unit selected");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            if (!GroundCombatContext.TryReloadActiveWeapon(unit!, out var reason))
            {
                host.AppendLine($"reload refused: {reason}");
                return;
            }
            host.AppendLine($"reloaded: {unit}");
            Log.Info($"{LogPrefix} reload: unit={unit}");
        }

        public static void ExecuteSpawn(string[] args, DevConsoleHost host)
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
            BuiltinCommands.WarnOnce(host);
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

        public static void ExecuteTeleport(string[] args, DevConsoleHost host)
        {
            if (!Args.RequireArgs(args, 0, host, "teleport"))
                return;
            if (!GroundCombatContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in GroundCombat");
                return;
            }
            if (!GroundCombatContext.TryGetSelectedUnit(world, out var unit))
            {
                host.AppendLine("no unit selected");
                return;
            }
            if (!GroundCombatContext.TryGetCursorPick(world, out _, out var address))
            {
                host.AppendLine("no cursor pick yet (move the mouse over a tile first)");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            if (!GroundCombatContext.TryTeleportToCursor(world, unit!, address, out var reason))
            {
                host.AppendLine($"teleport refused: {reason}");
                return;
            }
            host.AppendLine($"teleported: {unit} to ({address.i},{address.j})");
            Log.Info($"{LogPrefix} teleport: unit={unit} to=({address.i},{address.j})");
        }

        public static void ExecuteXray(string[] args, DevConsoleHost host)
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
                BuiltinCommands.WarnOnce(host);
            if (!GroundCombatContext.SetXray(world, nextOn))
            {
                host.AppendLine("could not toggle xray (visibility system missing)");
                return;
            }
            _xrayOn = nextOn;
            host.AppendLine($"xray: {(_xrayOn ? "on" : "off")}");
            Log.Info($"{LogPrefix} xray: {(_xrayOn ? "on" : "off")}");
        }

        private static string FormatLabel(string? species, string? rank)
        {
            if (species == null)
                return "auto";
            return rank == null ? species : $"{species}/{rank}";
        }
    }
}
