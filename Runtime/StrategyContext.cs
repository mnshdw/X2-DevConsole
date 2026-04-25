using System;
using Artitas;
using Common.Components;
using Common.Mechanics.Factions;
using Common.Modding;
using Common.RPG;
using Strategy.Components.Ranges;
using Xenonauts;
using Xenonauts.Strategy.Components;
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
    }
}
