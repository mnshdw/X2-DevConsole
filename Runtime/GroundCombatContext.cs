using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Artitas;
using Artitas.Core.Utils;
using Common;
using Common.Boards;
using Common.Components;
using Common.Mechanics.Factions;
using Common.Modding;
using UnityEngine;
using Xenonauts;
using Xenonauts.Common.Components;
using Xenonauts.Common.Components.Combatant;
using Xenonauts.GroundCombat;
using Xenonauts.GroundCombat.Animation.Acts;
using Xenonauts.GroundCombat.Components.Groups;
using Xenonauts.GroundCombat.Systems;
using Xenonauts.Strategy.Data;
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

        public static bool TryGetCursorPick(World world, out Entity? entity, out Address address)
        {
            entity = null;
            address = default;
            var tracker = world.GetSystem<PickingTracker>();
            if (tracker == null || !tracker.HasPick)
                return false;
            entity = tracker.LastPickedEntity;
            address = tracker.LastPickedAddress;
            return true;
        }

        public static AlignmentComponent.Alignment AlignmentToXenonauts(World world, Entity target)
        {
            var player = world.GetPlayer(XenonautsConstants.Players.XENONAUT);
            return target.AlignmentTo(player);
        }

        // Kills any alive combatant under the cursor, regardless of alignment.
        public static bool TryKillCombatant(World world, Entity target, out string reason)
        {
            if (target == null)
            {
                reason = "no target";
                return false;
            }
            if (!target.HasLifeStatus())
            {
                reason = "target has no life status (not a combatant?)";
                return false;
            }
            if (!target.LifeStatus().IsAlive())
            {
                reason = "target is already dead or incapacitated";
                return false;
            }
            DeathAct.TriggerDeathStart(world, target);
            reason = "";
            return true;
        }

        // Synthesises a temp Spawner entity at the cursor address, dispatches SpawnCombatantCommand,
        // returns the first spawned entity. The temp entity is deleted in the finally block.
        public static bool TrySpawnHostileAt(
            World world,
            Address address,
            string? speciesName,
            string? rankName,
            out Entity? spawned,
            out string reason
        )
        {
            spawned = null;
            if (address.addressType != AddressType.Centre)
            {
                reason = "cursor address is not a tile centre";
                return false;
            }
            Entity? spawner = null;
            try
            {
                spawner = world.CreateEntity();
                spawner.Add(new AddressComponent { value = address });
                spawner.Add(
                    new FootprintComponent
                    {
                        value = new HashSet<Address> { address },
                        Bounds = new Bounds(
                            new Vector3(address.i, address.j, address.floor),
                            Vector3.one
                        ),
                        isValid = true,
                        isAllLegal = true,
                    }
                );
                var producer = BuildAlienProducer(
                    world,
                    speciesName,
                    rankName,
                    out var buildReason
                );
                if (producer == null)
                {
                    reason = buildReason;
                    Log.Error($"{LogPrefix} spawn aborted: {reason}");
                    return false;
                }
                var command = new SpawnCombatantCommand
                {
                    Player = XenonautsConstants.Players.ALIEN,
                    Producer = producer,
                    Spawner = spawner,
                };
                world.HandleEvent(command);
                if (command.Spawned == null || command.Spawned.Count == 0)
                {
                    reason =
                        "spawn produced no entity (template lookup failed or tile illegal); see output.log";
                    Log.Error($"{LogPrefix} {reason} (species={speciesName ?? "auto"})");
                    return false;
                }
                spawned = command.Spawned[0];
                reason = "";
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"{LogPrefix} TrySpawnHostileAt threw: {ex}");
                reason = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
            finally
            {
                if (spawner != null)
                {
                    spawner.Delete();
                }
            }
        }

        // Builds an alien-species producer. Resolution order:
        //   1. If user provided species AND rank, look up the exact combo in LoadoutRegistry.
        //   2. If user provided species (no rank), use the first known combo for that species.
        //   3. Otherwise sample from any alive hostile combatant on the map.
        // Returns null if none of the above produces a viable triple.
        private static IReference<ITemplateProducer>? BuildAlienProducer(
            World world,
            string? requestedSpecies,
            string? requestedRank,
            out string reason
        )
        {
            reason = "";
            Species species;
            RoleGroup group;
            Rank rank;
            Xenonauts.Common.Components.Gender gender;
            Ethnicity ethnicity;

            if (
                requestedSpecies != null
                && requestedRank != null
                && LoadoutRegistry.TryGetWithRank(
                    requestedSpecies.ToLowerInvariant(),
                    requestedRank.ToLowerInvariant(),
                    out var exact
                )
            )
            {
                species = (Species)requestedSpecies.ToLowerInvariant();
                group = exact.Group;
                rank = exact.Rank;
                gender = exact.Gender;
                ethnicity = exact.Ethnicity;
            }
            else if (
                requestedSpecies != null
                && LoadoutRegistry.TryGet(requestedSpecies.ToLowerInvariant(), out var combos)
                && combos.Count > 0
            )
            {
                var combo = combos[0];
                species = (Species)requestedSpecies.ToLowerInvariant();
                group = combo.Group;
                rank =
                    requestedRank != null
                        ? new Rank { value = requestedRank.ToLowerInvariant() }
                        : combo.Rank;
                gender = combo.Gender;
                ethnicity = combo.Ethnicity;
            }
            else
            {
                var sample = FindSampleAlien(world);
                if (sample == null || !sample.HasBodyIdentifier())
                {
                    reason =
                        requestedSpecies != null
                            ? $"species '{requestedSpecies}' not in loadout registry and no alien on the map to sample"
                            : "no alien on the map to sample (and no species given)";
                    return null;
                }
                var body = sample.BodyIdentifier();
                species =
                    requestedSpecies != null
                        ? (Species)requestedSpecies.ToLowerInvariant()
                        : body.species;
                group = body.group;
                gender = body.gender;
                ethnicity = body.ethnicity;
                rank =
                    requestedRank != null
                        ? new Rank { value = requestedRank.ToLowerInvariant() }
                        : (sample.HasRank() ? sample.Rank() : Rank.Default());
            }

            var producer = new CombatantProducer
            {
                species = species,
                ethnicity = ethnicity,
                group = group,
                rank = rank,
                gender = gender,
                quantity = new XRange(1f, 1f),
            };
            return Reference.Live((ITemplateProducer)producer);
        }

        // Finds any alive hostile combatant in the world. Used as a fallback when the loadout
        // registry doesn't know the requested species or none was given.
        private static Entity? FindSampleAlien(World world)
        {
            var xen = world.GetPlayer(XenonautsConstants.Players.XENONAUT);
            if (xen == null)
                return null;
            return world
                .FindGroup<CombatantsGroup>()
                .FirstOrDefault(c =>
                    c.HasLifeStatus()
                    && c.LifeStatus().IsAlive()
                    && c.HasBodyIdentifier()
                    && c.AlignmentTo(xen) == AlignmentComponent.Alignment.Hostile
                );
        }

        // Sets SightingStateModelVisibilitySystem.Mode and forces an immediate re-evaluation of
        // every tracked actor (the public Mode setter only iterates when going to RevealAll;
        // going back to Normal silently leaves currently-shown actors visible until the next
        // turn refresh, which we don't want for an interactive debug toggle).
        // Returns false if the system is missing on this world.
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
