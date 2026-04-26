using System;
using System.Collections.Generic;
using System.Linq;
using Common.Content;
using Common.Content.DataStructures;
using Xenonauts.Common.Assets.Identifiers;
using Xenonauts.Common.Components;
using Xenonauts.Common.Components.Combatant;
using Xenonauts.Strategy;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime
{
    // Walks StrategyConstants.LOADOUTS to discover the (species, group, rank) triples the game
    // actually has loadouts for. Used by the spawn command so we don't have to guess. Built lazily
    // on first access; if the content manager isn't ready yet (registry empty) we don't cache,
    // so a later call will try again.
    public static class LoadoutRegistry
    {
        public sealed class Combo
        {
            public RoleGroup Group = RoleGroup.Default();
            public Rank Rank = Rank.Default();
            public Xenonauts.Common.Components.Gender Gender =
                Xenonauts.Common.Components.Gender.Male();
            public Ethnicity Ethnicity = Ethnicity.Default();
        }

        private static Dictionary<string, List<Combo>>? _cache;

        public static IReadOnlyDictionary<string, List<Combo>> All =>
            _cache ??= TryBuild() ?? new Dictionary<string, List<Combo>>();

        public static bool TryGet(string speciesValue, out List<Combo> combos)
        {
            return All.TryGetValue(speciesValue, out combos!);
        }

        public static bool TryGetWithRank(string speciesValue, string rankValue, out Combo combo)
        {
            combo = null!;
            if (!All.TryGetValue(speciesValue, out var combos))
                return false;
            foreach (var c in combos)
            {
                if (string.Equals(c.Rank?.value, rankValue, StringComparison.OrdinalIgnoreCase))
                {
                    combo = c;
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<string> RanksFor(string speciesValue)
        {
            if (!All.TryGetValue(speciesValue, out var combos))
                yield break;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in combos)
            {
                var r = c.Rank?.value ?? "default";
                if (seen.Add(r))
                    yield return r;
            }
        }

        public static IEnumerable<string> Species => All.Keys;

        // Drops the cache so a future call re-walks LOADOUTS. Useful if first walk happened
        // before the content manager finished loading.
        public static void Invalidate()
        {
            _cache = null;
        }

        private static Dictionary<string, List<Combo>>? TryBuild()
        {
            if (!AssetReference.IsContentManagerActive)
            {
                Log.Info(
                    $"{LogPrefix} LoadoutRegistry: ContentManager not active yet, will retry on next access"
                );
                return null;
            }
            var result = new Dictionary<string, List<Combo>>(StringComparer.OrdinalIgnoreCase);
            var loaded = 0;
            var fromPath = 0;
            var failed = 0;
            // The content manager defaults to Strict mode where Get() throws unless Load was
            // called first. ReinforcementsSystem uses the same Immediate trick to bypass that.
            using (AssetReference.AssetConfig.TemporarilySetLoadingRule(LoadingRule.Immediate))
            {
                foreach (var assetRef in StrategyConstants.LOADOUTS)
                {
                    if (TryReadComponents(assetRef, out var combo, out var speciesValue))
                    {
                        AddCombo(result, speciesValue, combo);
                        loaded++;
                        continue;
                    }
                    if (TryParsePath(assetRef, out combo, out speciesValue))
                    {
                        AddCombo(result, speciesValue, combo);
                        fromPath++;
                        continue;
                    }
                    failed++;
                }
            }
            Log.Info(
                $"{LogPrefix} LoadoutRegistry: {loaded} loaded + {fromPath} via path-parse + {failed} failed; {result.Count} species ({string.Join(", ", result.Keys.OrderBy(k => k))})"
            );
            return result.Count == 0 ? null : result;
        }

        private static bool TryReadComponents(
            AssetReference<Artitas.Template> assetRef,
            out Combo combo,
            out string speciesValue
        )
        {
            combo = null!;
            speciesValue = null!;
            try
            {
                var template = assetRef.Get();
                if (template == null)
                    return false;
                if (!template.Has<BodyIdentifierComponent>())
                    return false;
                var body = template.Get<BodyIdentifierComponent>();
                if (body?.species == null || string.IsNullOrEmpty(body.species.value))
                    return false;
                speciesValue = body.species.value;
                combo = new Combo
                {
                    Group = body.group ?? RoleGroup.Default(),
                    Rank = template.Has<Rank>() ? template.Get<Rank>() : Rank.Default(),
                    Gender = body.gender ?? Xenonauts.Common.Components.Gender.Male(),
                    Ethnicity = body.ethnicity ?? Ethnicity.Default(),
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Path parsing as a fallback. Loadout files are named like
        //   wraith^default^soldier^fusion.json
        // i.e. {species}^{group}^{rank}^{variant}. The first three segments are what we need.
        // Files with fewer than three ^-segments (e.g. xenonaut_gc^rifleman.json) are skipped
        // since we can't unambiguously identify (group, rank).
        private static bool TryParsePath(
            AssetReference<Artitas.Template> assetRef,
            out Combo combo,
            out string speciesValue
        )
        {
            combo = null!;
            speciesValue = null!;
            try
            {
                var rel = assetRef.Descriptor?.AsAssetDescriptor()?.GetRelativePath();
                if (string.IsNullOrEmpty(rel))
                    return false;
                var name = System.IO.Path.GetFileNameWithoutExtension(rel);
                var parts = name.Split('^');
                if (parts.Length < 3)
                    return false;
                speciesValue = parts[0];
                combo = new Combo
                {
                    Group = new RoleGroup { value = parts[1] },
                    Rank = new Rank { value = parts[2] },
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AddCombo(
            Dictionary<string, List<Combo>> map,
            string species,
            Combo combo
        )
        {
            if (!map.TryGetValue(species, out var list))
            {
                list = new List<Combo>();
                map[species] = list;
            }
            // De-dup by (group, rank).
            foreach (var c in list)
            {
                if (
                    string.Equals(
                        c.Group?.value,
                        combo.Group?.value,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && string.Equals(
                        c.Rank?.value,
                        combo.Rank?.value,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return;
                }
            }
            list.Add(combo);
        }
    }
}
