using Artitas;
using Artitas.Systems;
using Artitas.Utils;
using Common.Boards;
using Common.Input;
using Xenonauts.GroundCombat.Picking;

namespace DevConsole.Runtime
{
    // Subscribes to MouseHoverReport and caches the latest GCPickingReport so the console
    // commands can read what's under the cursor without doing their own raycasts.
    // Registered on the GroundCombat world from DevConsoleLifecycle.OnWorldCreate.
    public sealed class PickingTracker : EventSystem
    {
        public Entity? LastPickedEntity { get; private set; }
        public Address LastPickedAddress { get; private set; }
        public bool HasPick { get; private set; }

        [Subscriber]
        public void OnHover(MouseHoverReport report)
        {
            var pick = report.GetFirstReport<GCPickingReport>();
            if (!pick.IsSet)
                return;
            var p = pick.Value;
            LastPickedEntity = p.PickedEntity;
            LastPickedAddress = p.PickedAddress;
            HasPick = true;
        }
    }
}
