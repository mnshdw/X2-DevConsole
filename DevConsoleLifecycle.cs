using System;
using System.Collections.Generic;
using Artitas;
using Common.Content;
using Common.Modding;
using DevConsole.Runtime;
using HarmonyLib;
using UnityEngine;
using static DevConsole.ModConstants;

namespace DevConsole
{
    public class DevConsoleLifecycle : IModLifecycle
    {
        private const string HostObjectName = "DevConsole.Host";
        private GameObject? _host;

        public void Create(Mod mod, Harmony patcher)
        {
            Log.Info($"{LogPrefix} Create - mod loaded");
            try
            {
                _host = new GameObject(HostObjectName);
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _host.AddComponent<DevConsoleHost>();
                Log.Info($"{LogPrefix} host GameObject attached");
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} failed to create host GameObject: {ex}");
            }
        }

        public void Destroy()
        {
            Log.Info($"{LogPrefix} Destroy - mod unloaded");
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }
            GameContext.Clear();
        }

        public void OnWorldCreate(IModLifecycle.Section section, WeakReference<World> world)
        {
            Log.Info($"{LogPrefix} OnWorldCreate - section={section}");
            GameContext.RegisterWorld(section, world);

            if (section == IModLifecycle.Section.GroundCombat && world.TryGetTarget(out var w))
            {
                try
                {
                    w.RegisterSystem<PickingTracker>();
                    Log.Info($"{LogPrefix} PickingTracker registered on GroundCombat world");
                }
                catch (Exception ex)
                {
                    Log.Error($"{LogPrefix} failed to register PickingTracker: {ex}");
                }
            }
        }

        public IEnumerable<Descriptor> GetRequiredAssets(IModLifecycle.Section section)
        {
            return [];
        }

        public void OnWorldDispose(IModLifecycle.Section section, WeakReference<World> world)
        {
            Log.Info($"{LogPrefix} OnWorldDispose - section={section}");
            GameContext.UnregisterWorld(section);
        }
    }
}
