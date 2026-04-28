using System;
using System.Collections.Generic;
using Common.Components;
using Xenonauts.Common.Stats;
using Xenonauts.Strategy.Components;

namespace DevConsole.Runtime.Stats
{
    public static class AircraftStatTable
    {
        public static readonly IReadOnlyList<StatEntry> All = new[]
        {
            new StatEntry(
                "health",
                new[] { new StatChange(typeof(HitPoints)) },
                typeof(HitPoints),
                true,
                true
            ),
            new StatEntry("fuel", new[] { new StatChange(typeof(Fuel)) }, typeof(Fuel), true, true),
            new StatEntry(
                "armor",
                new[] { new StatChange(typeof(AirCombatArmorComponent)) },
                typeof(AirCombatArmorComponent),
                true,
                true
            ),
            new StatEntry(
                "speed",
                new[] { new StatChange(typeof(Speed)) },
                typeof(Speed),
                true,
                true
            ),
            new StatEntry(
                "turnspeed",
                new[] { new StatChange(typeof(AirCombatTurnSpeedComponent)) },
                typeof(AirCombatTurnSpeedComponent),
                true,
                true
            ),
        };

        public static bool TryFind(string name, out StatEntry entry)
        {
            foreach (var e in All)
            {
                if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
