using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    public static class BuiltinCommands
    {
        private static bool _warnedThisSession;

        public static void RegisterAll()
        {
            // Geoscape commands

            CommandRegistry.Register(
                "funds",
                Scene.Geoscape,
                StrategyCommands.ExecuteFunds,
                "funds <delta>",
                "add or remove Cash (eg. funds 5000, funds -1000)"
            );

            CommandRegistry.Register(
                "op",
                Scene.Geoscape,
                StrategyCommands.ExecuteOp,
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
                StrategyCommands.ExecuteAircraft,
                "aircraft <cmd> [args]",
                "aircraft add <type> [base] - spawn an aircraft into an empty hangar",
                "aircraft add ? lists aircraft types"
            );

            CommandRegistry.Register(
                "restore",
                Scene.Geoscape,
                RestoreCommand.Execute,
                "restore <stat|all> <name>",
                "restore stats by name",
                "soldier: health, timeunits",
                "aircraft: health, fuel, armor",
                "restore ? lists stats"
            );

            CommandRegistry.Register(
                "engineering",
                Scene.Geoscape,
                StrategyCommands.ExecuteEngineering,
                "engineering complete [<name>|all] - finish engineering projects",
                "engineering ? for details"
            );

            CommandRegistry.Register(
                "research",
                Scene.Geoscape,
                StrategyCommands.ExecuteResearch,
                "research complete [<name>|all] - finish research projects",
                "research ? for details"
            );

            // GroundCombat commands

            CommandRegistry.Register(
                "stun",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteStun,
                "stun",
                "knock out the combatant under the mouse cursor"
            );

            CommandRegistry.Register(
                "stunall",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteStunAll,
                "stunall",
                "knock out every alien on the map"
            );

            CommandRegistry.Register(
                "kill",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteKill,
                "kill",
                "kill the combatant under the mouse cursor"
            );

            CommandRegistry.Register(
                "killall",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteKillAll,
                "killall",
                "kill every conscious alien on the map"
            );

            CommandRegistry.Register(
                "reload",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteReload,
                "reload",
                "refill the selected unit's loaded magazine to its maximum"
            );

            CommandRegistry.Register(
                "restore",
                Scene.GroundCombat,
                RestoreCommand.Execute,
                "restore <stat|all>",
                "restore stats for the combatant under the mouse cursor",
                "restore ? lists stats"
            );

            CommandRegistry.Register(
                "spawn",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteSpawn,
                "spawn [species [rank]]",
                "spawn an alien on the tile under the mouse cursor",
                "with no arg, copies from an alien on the map",
                "spawn ? lists species",
                "spawn <species> ? lists ranks"
            );

            CommandRegistry.Register(
                "teleport",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteTeleport,
                "teleport",
                "snap the selected unit to the tile under the mouse cursor",
                "does not refresh LoS/cover; they update on next move or turn"
            );

            CommandRegistry.Register(
                "xray",
                Scene.GroundCombat,
                GroundCombatCommands.ExecuteXray,
                "xray",
                "toggle X-ray vision on enemy silhouettes (does not lift fog of war)"
            );
        }

        // Shared one-shot warning emitted by any state-mutating command on first use.
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
