using System.Linq;
using Artitas;
using Xenonauts.Common.Components.Item;
using Xenonauts.Common.Systems;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime.Commands
{
    public static class ItemCommand
    {
        public static void Execute(string[] args, DevConsoleHost host)
        {
            if (args.Length == 0 || args[0] == "help")
            {
                host.AppendLine($"usage: {Sig("item <delta> <item> [base]")}");
                host.AppendLine($"  {Sig("item ? [filter]")} lists item paths and names, including corpses and mod items");
                host.AppendLine("  use an item ID/path or a single-word name fragment; base defaults to the first alive base");
                return;
            }
            if (!StrategyContext.TryGetWorld(out var world))
            {
                host.AppendLine("not in Geoscape");
                return;
            }
            var listing = args[0] == "?";
            var delta = 0;
            if (!listing)
            {
                if (args.Length < 2)
                {
                    host.AppendLine($"usage: {Sig("item <delta> <item> [base]")}");
                    return;
                }
                if (!Args.TryParseInt(args[0], out delta))
                {
                    host.AppendLine($"item: '{args[0]}' is not an integer");
                    return;
                }
            }
            var items = StrategyItemCatalog.Read(world, out var status);
            if (!string.IsNullOrEmpty(status))
            {
                host.AppendLine($"item: {status}");
                return;
            }
            var query = listing ? string.Join(" ", args.Skip(1)) : args[1];
            var matches = StrategyItemCatalog.Find(items, query, exactFirst: !listing);
            if (listing || matches.Count > 1)
            {
                host.AppendLine(listing ? $"items matching '{query}' ({matches.Count}):"
                    : $"item: '{query}' is ambiguous ({matches.Count} matches):");
                foreach (var item in matches.Take(50))
                    host.AppendLine($"  {item.Label}");
                if (matches.Count > 50)
                    host.AppendLine($"  ... {matches.Count - 50} more; narrow the filter with {Sig("item ? <filter>")}");
                return;
            }
            if (matches.Count == 0)
            {
                host.AppendLine($"item: no item matching '{query}' (try {Sig("item ? <filter>")})");
                return;
            }
            Entity? geoBase;
            if (args.Length > 2)
            {
                if (!StrategyContext.TryFindGeoBaseByName(world, string.Join(" ", args.Skip(2)), out geoBase, out var reason))
                {
                    host.AppendLine($"item: {reason}");
                    return;
                }
            }
            else
                geoBase = StrategyContext.PickAnyAliveGeoBase(world);
            if (geoBase == null)
            {
                host.AppendLine("item: no alive geo bases");
                return;
            }
            if (!geoBase.GetSlot(MainBaseSlots.Stores()).IsSet)
            {
                host.AppendLine("item: base has no stores slot");
                return;
            }
            BuiltinCommands.WarnOnce(host);
            var selected = matches[0];
            var result = StrategyContext.AddStoresItem(world, geoBase, selected.Reference, delta, out var newValue);
            if (result == StrategyContext.ResourceAddResult.NoStackToRemoveFrom)
            {
                host.AppendLine("item: nothing to remove (no stack exists yet)");
                return;
            }
            var baseLabel = geoBase.HasName() ? geoBase.Name().value : "?";
            host.AppendLine($"item ({baseLabel}): {selected.Label} {(delta >= 0 ? "+" : "")}{delta} => {newValue:N0}");
            Log.Info($"{LogPrefix} item base={baseLabel} path={selected.Path} delta={delta} new={newValue}");
        }
    }
}
