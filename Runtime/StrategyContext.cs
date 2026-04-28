using System;
using System.Collections.Generic;
using Artitas;
using Common.Components;
using Common.Mechanics.Factions;
using Common.Modding;
using Common.RPG;
using Strategy.Components.Ranges;
using Xenonauts;
using Xenonauts.Strategy.Components;
using Xenonauts.Strategy.Factories;
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
                Log.Error(
                    $"{LogPrefix} StrategyContext: no XENONAUT player entity in PlayersGroup"
                );
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
    }
}
