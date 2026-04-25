using System;
using System.Collections.Generic;
using System.Linq;
using Artitas;
using Common.Content;
using Common.Modding;
using HarmonyLib;
using static DevConsole.ModConstants;

namespace DevConsole {

    public class DevConsoleLifecycle : IModLifecycle {

        public void Create(Mod mod, Harmony patcher) {
            Log.Warn($"{LogPrefix} Create — mod loaded");
        }

        public void Destroy() {
            Log.Warn($"{LogPrefix} Destroy — mod unloaded");
        }

        public void OnWorldCreate(IModLifecycle.Section section, WeakReference<World> world) {
            Log.Warn($"{LogPrefix} OnWorldCreate — section={section}");
        }

        public IEnumerable<Descriptor> GetRequiredAssets(IModLifecycle.Section section) {
            return Enumerable.Empty<Descriptor>();
        }

        public void OnWorldDispose(IModLifecycle.Section section, WeakReference<World> world) {
            Log.Warn($"{LogPrefix} OnWorldDispose — section={section}");
        }
    }

}
