using System;
using System.Collections.Generic;
using System.Linq;
using Artitas;
using Artitas.Components;
using Artitas.Core.Utils;
using Common.Components;
using Common.Taxonomies;
using Common.Utilities.EqualityComparers;
using Xenonauts.Common.Systems;
using Xenonauts.Strategy.Components;

namespace DevConsole.Runtime
{
    public static class StrategyItemCatalog
    {
        public sealed class Item
        {
            public IReference<Template> Reference = null!;
            public string Path = "";
            public string Id = "";
            public string Name = "";
            public string Label => $"{Path} ({Name})";
        }

        // Use the live campaign requisition, including mod items and campaign upgrades.
        // Its references wrap templates; the original TemplateReference identifies store stacks.
        public static List<Item> Read(World world, out string status)
        {
            status = "";
            var result = new List<Item>();
            var campaign = world.GetSystem<CampaignLogicSystem>();
            if (campaign == null || !campaign.IsCampaignInitialized || campaign.Campaign == null)
            {
                status = "campaign not initialized yet";
                return result;
            }
            if (!campaign.Campaign.HasItemRequisition())
            {
                status = "campaign has no item requisition";
                return result;
            }
            foreach (var entry in campaign.Campaign.ItemRequisition())
            {
                var template = entry?.Get();
                var reference = template?.Get<TemplateReferenceComponent>()?.value;
                var path = reference?.GetAssetDescriptor()?.GetRelativePath();
                if (reference == null || string.IsNullOrEmpty(path))
                    continue;
                result.Add(new Item
                {
                    Reference = reference,
                    Path = path!.Replace('\\', '/'),
                    Id = System.IO.Path.GetFileNameWithoutExtension(path),
                    Name = template!.Get<NameComponent>()?.value ?? "",
                });
            }
            if (result.Count == 0)
                status = "campaign item requisition is empty";
            return Deduplicate(result).OrderBy(i => i.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<Item> Deduplicate(IEnumerable<Item> items)
        {
            // RequisitionComponent.Get uses the first entry with this reference identity.
            // Multiple wrappers can name the same item differently (Captured/Stunned Wraith).
            // Use the game's comparer, not filenames, so distinct mod assets stay distinct.
            return items
                .GroupBy(i => i.Reference, ReferenceIdentityEqualityComparer<Template>.Default)
                .Select(group => group.First())
                .ToList();
        }

        public static List<Item> Find(List<Item> items, string query, bool exactFirst)
        {
            query = query.Replace('\\', '/');
            if (exactFirst)
            {
                // A full path disambiguates items with the same filename or display name.
                var paths = items.Where(i => string.Equals(i.Path, query, StringComparison.OrdinalIgnoreCase)).ToList();
                if (paths.Count > 0)
                    return paths;
                var exact = items.Where(i => string.Equals(i.Id, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(i.Name, query, StringComparison.OrdinalIgnoreCase)).ToList();
                if (exact.Count > 0)
                    return exact;
            }
            return items.Where(i => i.Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || i.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }
    }
}
