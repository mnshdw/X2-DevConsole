using System;
using System.Collections.Generic;
using Artitas;
using Common.Modding;
using static DevConsole.ModConstants;

namespace DevConsole.Runtime
{
    public static class GameContext
    {
        private static readonly Dictionary<IModLifecycle.Section, WeakReference<World>> Worlds = [];
        private static readonly HashSet<IModLifecycle.Section> SeenSections = [];

        public static bool ConsoleHasFocus { get; set; }

        public static void RegisterWorld(IModLifecycle.Section section, WeakReference<World> world)
        {
            Worlds[section] = world;
            if (SeenSections.Add(section))
            {
                Log.Info($"{LogPrefix} GameContext: first sighting of section={section}");
            }
        }

        public static void UnregisterWorld(IModLifecycle.Section section)
        {
            Worlds.Remove(section);
        }

        public static bool TryGetWorld(IModLifecycle.Section section, out World world)
        {
            if (Worlds.TryGetValue(section, out var weak) && weak.TryGetTarget(out world!))
            {
                return true;
            }
            world = null!;
            return false;
        }

        public static IEnumerable<IModLifecycle.Section> ActiveSections()
        {
            foreach (var kv in Worlds)
            {
                if (kv.Value.TryGetTarget(out _))
                    yield return kv.Key;
            }
        }

        public static void Clear()
        {
            Worlds.Clear();
        }
    }
}
