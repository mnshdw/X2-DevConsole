using System;
using Artitas;
using HarmonyLib;
using Xenonauts.Strategy.UI.Components.Project;

namespace DevConsole.Runtime.Patches
{
    // ProjectQueueRowController.OnDispose forgets to unregister the SetTimeToCompleteText listener
    // it registers on Quantity in OnTargetUpdated, so a recycled row can fire the listener after
    // its base.Target has been cleared.
    //
    // The listener then calls SetTimeToCompleteText(GetTargetProjectQueue(), task), and the
    // (Entity, Entity) overload NREs on projectQueue.IsInProjectQueueStateMachine.
    //
    // Without this patch, mutating a queued project task's ProgressPoints from outside the normal
    // tick (`research complete` / `engineering complete` commands) crashes the world. Skip the
    // original when projectQueue is null, it just means the "time to complete" text can't update
    // on a dead row, which is harmless in theory.
    [HarmonyPatch]
    public static class ProjectQueueRowControllerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(ProjectQueueRowController),
            "SetTimeToCompleteText",
            new Type[] { typeof(Entity), typeof(Entity) }
        )]
        public static bool SetTimeToCompleteText_Prefix(Entity projectQueue)
        {
            return projectQueue != null;
        }
    }
}
