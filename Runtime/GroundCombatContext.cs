using System.Reflection;
using Artitas;
using Common.Modding;
using Xenonauts.GroundCombat;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime
{
    /// GroundCombat-world helpers.
    public static class GroundCombatContext
    {
        public static bool TryGetWorld(out World world)
        {
            return GameContext.TryGetWorld(IModLifecycle.Section.GroundCombat, out world);
        }

        /// Sets SightingStateModelVisibilitySystem.Mode and forces an immediate re-evaluation
        /// of every tracked actor (the public Mode setter only iterates when going to RevealAll;
        /// going back to Normal silently leaves currently-shown actors visible until the next
        /// turn refresh, which we don't want for an interactive debug toggle).
        /// Returns false if the system is missing on this world.
        public static bool SetXray(World world, bool on)
        {
            var sys = world.GetSystem<SightingStateModelVisibilitySystem>();
            if (sys == null)
            {
                Log.Error(
                    $"{LogPrefix} GroundCombatContext: SightingStateModelVisibilitySystem not registered on world"
                );
                return false;
            }

            sys.Mode = on
                ? SightingStateModelVisibilitySystem.Modes.RevealAll
                : SightingStateModelVisibilitySystem.Modes.Normal;

            // The setter iterates only when transitioning into RevealAll. For the off path we
            // reflectively iterate the system's private _hideableActors family and call its
            // private SetModelVisibilityBasedOnStateOrMode per entity.
            if (!on)
            {
                ForceVisibilityRefresh(sys);
            }
            return true;
        }

        private static void ForceVisibilityRefresh(SightingStateModelVisibilitySystem sys)
        {
            var t = typeof(SightingStateModelVisibilitySystem);
            var actorsField = t.GetField(
                "_hideableActors",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            var setVis = t.GetMethod(
                "SetModelVisibilityBasedOnStateOrMode",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (actorsField == null || setVis == null)
            {
                Log.Error(
                    $"{LogPrefix} GroundCombatContext: visibility-system internals moved (xray-off won't refresh until next turn)"
                );
                return;
            }
            var actors = actorsField.GetValue(sys) as Family;
            if (actors == null)
                return;

            var args = new object[2];
            foreach (Entity actor in actors)
            {
                args[0] = actor;
                args[1] = actor.SightingState();
                setVis.Invoke(sys, args);
            }
        }
    }
}
