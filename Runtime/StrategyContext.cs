using System;
using System.Collections.Generic;
using System.Linq;
using Artitas;
using Common.Components;
using Common.Mechanics.Factions;
using Common.Modding;
using Common.RPG;
using Strategy.Components.Ranges;
using Xenonauts;
using Xenonauts.Common.Systems;
using Xenonauts.Strategy.Components;
using Xenonauts.Strategy.Factories;
using Xenonauts.Strategy.Systems;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime
{
    /// Strategy-world helpers. All accessors run on the active Strategy world via GameContext.
    public static class StrategyContext
    {
        public static bool TryGetWorld(out World world)
        {
            return GameContext.TryGetWorld(IModLifecycle.Section.Strategy, out world);
        }

        public static bool TryGetPlayer(World world, out Entity player)
        {
            player = world.GetPlayer(XenonautsConstants.Players.XENONAUT);
            if (player == null)
            {
                Log.Error($"{LogPrefix} StrategyContext: XENONAUT player not found");
                return false;
            }
            return true;
        }

        /// Adds delta to the player's Cash. Returns the new value, or null if the component is absent.
        public static float? AddCash(Entity player, float delta)
        {
            return AddRange(player, typeof(Cash), delta);
        }

        /// Adds delta to the player's OperationPoints. Returns the new value, or null if absent.
        public static float? AddOp(Entity player, float delta)
        {
            return AddRange(player, typeof(OperationPoints), delta);
        }

        private static float? AddRange(Entity player, Type componentType, float delta)
        {
            if (!player.Has(componentType))
                return null;
            var clone = (RangeComponent)player.Get(componentType).Clone();
            RangeModifier.Modify(clone, RangeModifier.Operation.Additive, delta, 0f, 0f);
            player.Add(clone);
            return clone.Value;
        }

        public static IEnumerable<Entity> EnumerateActors(World world)
        {
            return world.RegisterFamily(StrategyArchetypes.StrategyActor);
        }

        public static IEnumerable<Entity> EnumerateAircraft(World world)
        {
            return world.RegisterFamily(StrategyAircraftArchetypes.StrategyAircraft);
        }

        // Case-insensitive: exact match wins; otherwise returns every actor whose name
        // contains the query as a substring.
        public static List<Entity> FindActorsByName(World world, string query) =>
            FindByName(EnumerateActors(world), query);

        public static List<Entity> FindAircraftByName(World world, string query) =>
            FindByName(EnumerateAircraft(world), query);

        public class NamedMatches
        {
            public List<Entity> Actors { get; }
            public List<Entity> Aircraft { get; }
            public int Total => Actors.Count + Aircraft.Count;

            public NamedMatches(List<Entity> actors, List<Entity> aircraft)
            {
                Actors = actors;
                Aircraft = aircraft;
            }
        }

        public static NamedMatches FindNamed(World world, string query) =>
            new NamedMatches(FindActorsByName(world, query), FindAircraftByName(world, query));

        private static List<Entity> FindByName(IEnumerable<Entity> entities, string query)
        {
            var exact = new List<Entity>();
            var partial = new List<Entity>();
            foreach (Entity entity in entities)
            {
                if (!entity.HasName())
                    continue;
                var name = entity.Name().value;
                if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
                    exact.Add(entity);
                else if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add(entity);
            }
            return exact.Count > 0 ? exact : partial;
        }

        // Adds delta to a range component. By default both Value and Maximum are
        // raised so the change persists as a real stat increase. With maxOnly=true
        // only Maximum changes (used for Stun, where Value is current stun damage).
        // Returns true if the component existed.
        public static bool AddStat(
            Entity actor,
            Type componentType,
            int delta,
            bool maxOnly = false
        )
        {
            if (!actor.Has(componentType))
                return false;
            var clone = (RangeComponent)actor.Get(componentType).Clone();
            var valueDelta = maxOnly ? 0f : (float)delta;
            RangeModifier.Modify(clone, RangeModifier.Operation.Additive, valueDelta, 0f, delta);
            actor.Add(clone);
            return true;
        }

        // Reads a range component's Value or Maximum. Used to display a number
        // that matches what the soldier sheet renders after a stat change.
        public static float? ReadStat(Entity actor, Type componentType, bool maximum)
        {
            if (!actor.Has(componentType))
                return null;
            var range = (RangeComponent)actor.Get(componentType);
            return maximum ? range.Maximum : range.Value;
        }

        // Raises a stat to its maximum.
        public static bool MaxStat(Entity actor, Type componentType, out bool changed)
        {
            changed = false;
            if (!actor.Has(componentType))
                return false;
            var clone = (RangeComponent)actor.Get(componentType).Clone();
            var delta = clone.Maximum - clone.Value;
            if (delta == 0f)
                return true;
            RangeModifier.Modify(clone, RangeModifier.Operation.Additive, delta, 0f, 0f);
            actor.Add(clone);
            changed = true;
            return true;
        }

        // Sets a stat to zero.
        public static bool ZeroStat(Entity actor, Type componentType, out bool changed)
        {
            changed = false;
            if (!actor.Has(componentType))
                return false;
            var clone = (RangeComponent)actor.Get(componentType).Clone();
            if (clone.Value == 0f)
                return true;
            RangeModifier.Modify(clone, RangeModifier.Operation.Additive, -clone.Value, 0f, 0f);
            actor.Add(clone);
            changed = true;
            return true;
        }

        // Aircraft templates: the campaign's full requisition list (entries that are locked
        // behind research are included). The UI filters by UnlockedStateMachineComponent, but
        // we don't so we can spawn aircrafts that aren't researched yet.

        public static List<Template> EnumerateAircraftTemplates(World world, out string status)
        {
            status = "";
            var result = new List<Template>();
            var campaign = world.GetSystem<CampaignLogicSystem>();
            if (campaign == null)
            {
                status = "CampaignLogicSystem not registered";
                return result;
            }
            if (!campaign.IsCampaignInitialized)
            {
                status = "campaign not initialized yet";
                return result;
            }
            var entity = campaign.Campaign;
            if (entity == null)
            {
                status = "campaign entity null";
                return result;
            }
            if (!entity.HasAircraftRequisition())
            {
                status = "campaign has no AircraftRequisitionComponent";
                return result;
            }
            foreach (var templateRef in entity.AircraftRequisition())
            {
                var template = templateRef?.Get();
                if (template != null)
                    result.Add(template);
            }
            if (result.Count == 0)
                status = "AircraftRequisition list is empty";
            return result;
        }

        public static List<string> ListAircraftTypeNames(World world, out string status)
        {
            var names = new List<string>();
            foreach (var template in EnumerateAircraftTemplates(world, out status))
            {
                if (!template.Has<XenonautAircraftType>())
                    continue;
                var type = template.Get<XenonautAircraftType>().value;
                if (!string.IsNullOrEmpty(type))
                    names.Add(type);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (names.Count == 0 && string.IsNullOrEmpty(status))
                status = "no template carries XenonautAircraftType";
            return names;
        }

        // Case-insensitive: exact match wins; otherwise unique substring match.
        // Returns null if zero or ambiguous matches; sets matched to the resolved type name.
        public static Template? FindAircraftTemplateByType(
            World world,
            string typeQuery,
            out string? matched,
            out List<string> ambiguous
        )
        {
            matched = null;
            ambiguous = new List<string>();
            Template? exact = null;
            string? exactType = null;
            var partial = new List<(Template, string)>();
            foreach (var template in EnumerateAircraftTemplates(world, out _))
            {
                if (!template.Has<XenonautAircraftType>())
                    continue;
                var type = template.Get<XenonautAircraftType>().value ?? "";
                if (string.Equals(type, typeQuery, StringComparison.OrdinalIgnoreCase))
                {
                    exact = template;
                    exactType = type;
                    break;
                }
                if (type.IndexOf(typeQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add((template, type));
            }
            if (exact != null)
            {
                matched = exactType;
                return exact;
            }
            if (partial.Count == 1)
            {
                matched = partial[0].Item2;
                return partial[0].Item1;
            }
            foreach (var (_, type) in partial)
                ambiguous.Add(type);
            return null;
        }

        public static List<Entity> EnumerateAliveGeoBases(World world)
        {
            var sys = world.GetSystem<GeoBaseSystem>();
            if (sys == null)
                return new List<Entity>();
            return sys.GetAliveGeoBases().ToList();
        }

        // Finds an empty enabled hangar in the given base, or returns null.
        public static Entity? FindEmptyHangarInGeoBase(World world, Entity geoBase)
        {
            var family = world.RegisterFamily(StrategyArchetypes.EmptyEnabledHangarOnSite);
            foreach (Entity hangar in family)
            {
                if (hangar.IsLinkedToBuildSite(geoBase))
                    return hangar;
            }
            return null;
        }

        // Picks (geoBase, hangar) for spawning. If geoBaseQuery is null, scans every alive
        // base; if it's set, matches by Name (exact or substring) and only considers that
        // base. Returns null reason on success.
        public static bool TryPickHangarForSpawn(
            World world,
            string? geoBaseQuery,
            out Entity? geoBase,
            out Entity? hangar,
            out string reason
        )
        {
            geoBase = null;
            hangar = null;
            reason = "";
            var bases = EnumerateAliveGeoBases(world);
            if (bases.Count == 0)
            {
                reason = "no alive geo bases";
                return false;
            }
            if (geoBaseQuery != null)
            {
                var matches = FindByName(bases, geoBaseQuery);
                if (matches.Count == 0)
                {
                    reason = $"no base matching '{geoBaseQuery}'";
                    return false;
                }
                if (matches.Count > 1)
                {
                    reason =
                        $"'{geoBaseQuery}' is ambiguous ({matches.Count} matches: "
                        + string.Join(", ", matches.Select(b => b.HasName() ? b.Name().value : "?"))
                        + ")";
                    return false;
                }
                geoBase = matches[0];
                hangar = FindEmptyHangarInGeoBase(world, geoBase);
                if (hangar == null)
                {
                    reason =
                        $"base '{(geoBase.HasName() ? geoBase.Name().value : "?")}' has no empty hangar";
                    return false;
                }
                return true;
            }
            foreach (var b in bases)
            {
                var h = FindEmptyHangarInGeoBase(world, b);
                if (h != null)
                {
                    geoBase = b;
                    hangar = h;
                    return true;
                }
            }
            reason = "no base with an empty hangar";
            return false;
        }

        // Spawns an aircraft of the given type into a hangar. Bypasses cost
        // and research/unlock gating. Returns the new aircraft entity.
        public static bool TrySpawnXenonautAircraft(
            World world,
            string typeQuery,
            string? geoBaseQuery,
            out Entity? aircraft,
            out string? matchedType,
            out string? matchedBase,
            out string reason
        )
        {
            aircraft = null;
            matchedType = null;
            matchedBase = null;
            reason = "";

            var template = FindAircraftTemplateByType(
                world,
                typeQuery,
                out matchedType,
                out var ambiguous
            );
            if (template == null)
            {
                if (ambiguous.Count > 0)
                    reason =
                        $"'{typeQuery}' is ambiguous ({ambiguous.Count} matches: "
                        + string.Join(", ", ambiguous)
                        + ")";
                else
                    reason = $"no aircraft type matching '{typeQuery}'";
                return false;
            }

            if (
                !TryPickHangarForSpawn(
                    world,
                    geoBaseQuery,
                    out var geoBase,
                    out var hangar,
                    out reason
                )
            )
                return false;
            matchedBase = geoBase!.HasName() ? geoBase.Name().value : null;

            if (!TryGetPlayer(world, out var player))
            {
                reason = "could not find player entity";
                return false;
            }

            var aircraftSystem = world.GetSystem<XenonautAircraftSystem>();
            if (aircraftSystem == null)
            {
                reason = "XenonautAircraftSystem missing";
                return false;
            }

            var spawned = aircraftSystem.GenerateAircraft(player, template);
            if (spawned == null)
            {
                reason = "GenerateAircraft returned null";
                return false;
            }

            // Bypass research/unlock gating in case the template is locked. Aircraft default
            // to Unlocked but a research-gated template stores Locked.
            if (
                spawned.HasUnlockedStateMachine()
                && spawned
                    .UnlockedStateMachine()
                    .IsInState(UnlockedStateMachineComponent.States.Locked)
                && spawned.CanTriggerUnlockedStateMachine(
                    UnlockedStateMachineComponent.Triggers.Unlock
                )
            )
            {
                spawned.TriggerUnlockedStateMachine(UnlockedStateMachineComponent.Triggers.Unlock);
            }

            aircraftSystem.RequisitionAircraft(
                player,
                hangar!,
                spawned,
                ignoreCost: true,
                delayed: false
            );

            aircraft = spawned;
            return true;
        }
    }
}
