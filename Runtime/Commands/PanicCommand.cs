using System;
using System.Linq;
using Common.RPG;
using Xenonauts.Strategy.Components;
using Xenonauts.Strategy.Factories;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    public static class PanicCommand
    {
        public static void Execute(string[] args, DevConsoleHost host)
        {
            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Geoscape");
                return;
            }
            // Snapshot before changing components: updates can change family membership.
            var regions = world.RegisterFamily(StrategyArchetypes.PoliticalGeoRegion)
                .OrderBy(r => r.GeoRegion().value.ToString()).ToList();
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "?" || args[0] == "help")))
            {
                host.AppendLine($"usage: {Sig("panic <delta> <region|all>")}");
                foreach (var region in regions)
                    host.AppendLine($"  {region.GeoRegion().value} ({region.Name().value}): {region.RegionalRelations().Value:N0} ({region.RelationshipStatus().value})");
                return;
            }
            if (args.Length < 2)
            {
                host.AppendLine($"usage: {Sig("panic <delta> <region|all>")}");
                return;
            }
            if (!Args.TryParseInt(args[0], out var delta))
            {
                host.AppendLine($"panic: '{args[0]}' is not an integer");
                return;
            }
            var query = string.Join(" ", args.Skip(1));
            if (!string.Equals(query, "all", StringComparison.OrdinalIgnoreCase))
            {
                var exact = regions.Where(r =>
                    string.Equals(r.GeoRegion().value.ToString(), query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.Name().value, query, StringComparison.OrdinalIgnoreCase)).ToList();
                regions = exact.Count > 0 ? exact : regions.Where(r =>
                    r.GeoRegion().value.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || r.Name().value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (regions.Count > 1)
                {
                    host.AppendLine($"panic: '{query}' is ambiguous: {string.Join(", ", regions.Select(r => r.GeoRegion().value))}");
                    return;
                }
            }
            if (regions.Count == 0)
            {
                host.AppendLine($"panic: no regions matching '{query}' (try {Sig("panic ?")})");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            foreach (var region in regions)
            {
                var current = region.RegionalRelations();
                var panic = new RegionalRelations(current.Minimum, current.Value, current.Maximum);
                var before = panic.Value;
                // Preserve the campaign's range limits and notify the game's region systems.
                RangeModifier.Modify(panic, RangeModifier.Operation.Additive, delta, 0f, 0f);
                region.Add(panic);
                host.AppendLine($"panic ({region.GeoRegion().value}): {before:N0} => {panic.Value:N0}");
                if (region.RelationshipStatus().Equals(RelationshipStatusComponent.Type.Lost))
                    host.AppendLine("  region is lost; recovery is evaluated by the game at the next monthly report (infiltration also matters)");
                Log.Info($"{LogPrefix} panic region={region.GeoRegion().value} delta={delta} before={before} new={panic.Value}");
            }
        }
    }
}
