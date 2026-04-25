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
                "[*] funds <delta> - adjust Cash on Geoscape (e.g. funds 5000, funds -1000)",
                ExecuteFunds
            );

            CommandRegistry.Register(
                "op",
                "[*] op <delta> - adjust Operation Points on Geoscape (e.g. op 5)",
                ExecuteOp
            );

            CommandRegistry.Register(
                "xray",
                "[*] xray - toggle X-ray vision on enemies in GroundCombat (silhouettes through walls)",
                ExecuteXray
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

        private static void WarnOnce(DevConsoleHost host)
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
