using System;
using System.Collections.Generic;
using Common.Components;
using Xenonauts.Common.Stats;

namespace DevConsole.Runtime.Stats
{
    // ComponentType: range component to mutate.
    // MaxOnly: true to leave Value untouched (e.g. Stun, where Value is current
    //          stun damage and only the ceiling tracks HP Max).
    public readonly struct StatChange
    {
        public readonly Type ComponentType;
        public readonly bool MaxOnly;

        public StatChange(Type componentType, bool maxOnly = false)
        {
            ComponentType = componentType;
            MaxOnly = maxOnly;
        }
    }

    // ReadType / ReadMaximum: which component and field the soldier sheet renders
    // for this stat. Used to print a number that matches the UI (hopefully).
    public readonly struct StatEntry
    {
        public readonly string Name;
        public readonly StatChange[] Changes;
        public readonly Type ReadType;
        public readonly bool ReadMaximum;
        public readonly bool IncludedInAll;

        public StatEntry(
            string name,
            StatChange[] changes,
            Type readType,
            bool readMaximum,
            bool includedInAll
        )
        {
            Name = name;
            Changes = changes;
            ReadType = readType;
            ReadMaximum = readMaximum;
            IncludedInAll = includedInAll;
        }
    }

    public static class SoldierStatTable
    {
        public static readonly IReadOnlyList<StatEntry> All = new[]
        {
            new StatEntry(
                "timeunits",
                new[] { new StatChange(typeof(TimeUnits)) },
                typeof(TimeUnits),
                true,
                true
            ),
            // Strategy doesn't re-derive HitPoints from UnmodifiedHitPoints, so all
            // three components must be raised together: base ceiling, current HP,
            // and the Stun ceiling that tracks HP Max.
            new StatEntry(
                "health",
                new[]
                {
                    new StatChange(typeof(UnmodifiedHitPoints)),
                    new StatChange(typeof(HitPoints)),
                    new StatChange(typeof(Stun), maxOnly: true),
                },
                typeof(HitPoints),
                true,
                true
            ),
            new StatEntry(
                "accuracy",
                new[] { new StatChange(typeof(Accuracy)) },
                typeof(Accuracy),
                false,
                true
            ),
            new StatEntry(
                "strength",
                new[] { new StatChange(typeof(Strength)) },
                typeof(Strength),
                false,
                true
            ),
            new StatEntry(
                "reflexes",
                new[] { new StatChange(typeof(Reflexes)) },
                typeof(Reflexes),
                false,
                true
            ),
            new StatEntry(
                "bravery",
                new[] { new StatChange(typeof(Bravery)) },
                typeof(Bravery),
                false,
                true
            ),
            new StatEntry(
                "psi",
                new[] { new StatChange(typeof(PsionicStrength)) },
                typeof(PsionicStrength),
                false,
                false
            ),
            new StatEntry(
                "capacity",
                new[] { new StatChange(typeof(Capacity)) },
                typeof(Capacity),
                false,
                false
            ),
            new StatEntry(
                "defence",
                new[] { new StatChange(typeof(Defence)) },
                typeof(Defence),
                false,
                false
            ),
            new StatEntry(
                "weight",
                new[] { new StatChange(typeof(Weight)) },
                typeof(Weight),
                false,
                false
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
