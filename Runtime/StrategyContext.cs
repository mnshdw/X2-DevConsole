using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Artitas;
using Artitas.Core.Utils;
using Artitas.Systems;
using Artitas.Utils;
using Common.Components;
using Common.Content;
using Common.Mechanics.Factions;
using Common.Modding;
using Common.RPG;
using Common.Taxonomies;
using Strategy.Components.Ranges;
using Xenonauts;
using Xenonauts.Common.Components.Item;
using Xenonauts.Common.Systems;
using Xenonauts.Strategy;
using Xenonauts.Strategy.Components;
using Xenonauts.Strategy.Data.EntityEffects;
using Xenonauts.Strategy.Data.EntitySelectors;
using Xenonauts.Strategy.Factories;
using Xenonauts.Strategy.Systems;
using Xenonauts.Strategy.Utils;
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
                Log.Error($"{LogPrefix} StrategyContext: XENONAUT player not found");
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

        public enum ResourceAddResult
        {
            Ok,
            NoStackToRemoveFrom,
        }

        // Adds delta to the player stores stack for the given item template (alenium, alloys).
        public static ResourceAddResult AddStoresItem(
            World world,
            Entity player,
            IReference<Template> itemReference,
            float delta,
            out float newValue
        )
        {
            newValue = 0f;
            var slot = MainBaseSlots.Stores();
            var existing = player.GetItem(itemReference, slot);
            if (existing.IsSet)
            {
                existing.Value.DeltaQuantity(delta);
                newValue = existing.Value.Quantity().Value;
                return ResourceAddResult.Ok;
            }
            if (delta <= 0f)
                return ResourceAddResult.NoStackToRemoveFrom;
            var entity = itemReference.Get().Wrap(new Quantity(delta)).Create(world);
            player.AddItem(slot, entity, mergeStacks: true);
            newValue = delta;
            return ResourceAddResult.Ok;
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

        public static IEnumerable<Entity> EnumerateActors(World world)
        {
            return world.RegisterFamily(StrategyArchetypes.StrategyActor);
        }

        public static IEnumerable<Entity> EnumerateAircraft(World world)
        {
            return world.RegisterFamily(StrategyAircraftArchetypes.StrategyAircraft);
        }

        // Case-insensitive: exact match wins; otherwise returns every actor whose name
        // contains the query as a substring.
        public static List<Entity> FindActorsByName(World world, string query) =>
            FindByName(EnumerateActors(world), query);

        public static List<Entity> FindAircraftByName(World world, string query) =>
            FindByName(EnumerateAircraft(world), query);

        public class NamedMatches
        {
            public List<Entity> Actors { get; }
            public List<Entity> Aircraft { get; }
            public int Total => Actors.Count + Aircraft.Count;

            public NamedMatches(List<Entity> actors, List<Entity> aircraft)
            {
                Actors = actors;
                Aircraft = aircraft;
            }
        }

        public static NamedMatches FindNamed(World world, string query) =>
            new NamedMatches(FindActorsByName(world, query), FindAircraftByName(world, query));

        private static List<Entity> FindByName(IEnumerable<Entity> entities, string query)
        {
            var exact = new List<Entity>();
            var partial = new List<Entity>();
            foreach (Entity entity in entities)
            {
                if (!entity.HasName())
                    continue;
                var name = entity.Name().value;
                if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
                    exact.Add(entity);
                else if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add(entity);
            }
            return exact.Count > 0 ? exact : partial;
        }

        // Adds delta to a range component. By default both Value and Maximum are
        // raised so the change persists as a real stat increase. With maxOnly=true
        // only Maximum changes (used for Stun, where Value is current stun damage).
        // Returns true if the component existed.
        public static bool AddStat(
            Entity actor,
            Type componentType,
            int delta,
            bool maxOnly = false
        )
        {
            if (!actor.Has(componentType))
                return false;
            var clone = (RangeComponent)actor.Get(componentType).Clone();
            var valueDelta = maxOnly ? 0f : (float)delta;
            RangeModifier.Modify(clone, RangeModifier.Operation.Additive, valueDelta, 0f, delta);
            actor.Add(clone);
            return true;
        }

        // Reads a range component's Value or Maximum. Used to display a number
        // that matches what the soldier sheet renders after a stat change.
        public static float? ReadStat(Entity actor, Type componentType, bool maximum)
        {
            if (!actor.Has(componentType))
                return null;
            var range = (RangeComponent)actor.Get(componentType);
            return maximum ? range.Maximum : range.Value;
        }

        // Raises a stat to its maximum.
        public static bool MaxStat(Entity actor, Type componentType, out bool changed)
        {
            changed = false;
            if (!actor.Has(componentType))
                return false;
            var clone = (RangeComponent)actor.Get(componentType).Clone();
            var delta = clone.Maximum - clone.Value;
            if (delta == 0f)
                return true;
            RangeModifier.Modify(clone, RangeModifier.Operation.Additive, delta, 0f, 0f);
            actor.Add(clone);
            changed = true;
            return true;
        }

        // Sets a stat to zero.
        public static bool ZeroStat(Entity actor, Type componentType, out bool changed)
        {
            changed = false;
            if (!actor.Has(componentType))
                return false;
            var clone = (RangeComponent)actor.Get(componentType).Clone();
            if (clone.Value == 0f)
                return true;
            RangeModifier.Modify(clone, RangeModifier.Operation.Additive, -clone.Value, 0f, 0f);
            actor.Add(clone);
            changed = true;
            return true;
        }

        // Aircraft templates: the campaign's full requisition list (entries that are locked
        // behind research are included). The UI filters by UnlockedStateMachineComponent, but
        // we don't so we can spawn aircrafts that aren't researched yet.

        public static List<Template> EnumerateAircraftTemplates(World world, out string status)
        {
            status = "";
            var result = new List<Template>();
            var campaign = world.GetSystem<CampaignLogicSystem>();
            if (campaign == null)
            {
                status = "CampaignLogicSystem not registered";
                return result;
            }
            if (!campaign.IsCampaignInitialized)
            {
                status = "campaign not initialized yet";
                return result;
            }
            var entity = campaign.Campaign;
            if (entity == null)
            {
                status = "campaign entity null";
                return result;
            }
            if (!entity.HasAircraftRequisition())
            {
                status = "campaign has no AircraftRequisitionComponent";
                return result;
            }
            foreach (var templateRef in entity.AircraftRequisition())
            {
                var template = templateRef?.Get();
                if (template != null)
                    result.Add(template);
            }
            if (result.Count == 0)
                status = "AircraftRequisition list is empty";
            return result;
        }

        public static List<string> ListAircraftTypeNames(World world, out string status)
        {
            var names = new List<string>();
            foreach (var template in EnumerateAircraftTemplates(world, out status))
            {
                if (!template.Has<XenonautAircraftType>())
                    continue;
                var type = template.Get<XenonautAircraftType>().value;
                if (!string.IsNullOrEmpty(type))
                    names.Add(type);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (names.Count == 0 && string.IsNullOrEmpty(status))
                status = "no template carries XenonautAircraftType";
            return names;
        }

        // Case-insensitive: exact match wins; otherwise unique substring match.
        // Returns null if zero or ambiguous matches; sets matched to the resolved type name.
        public static Template? FindAircraftTemplateByType(
            World world,
            string typeQuery,
            out string? matched,
            out List<string> ambiguous
        )
        {
            matched = null;
            ambiguous = new List<string>();
            Template? exact = null;
            string? exactType = null;
            var partial = new List<(Template, string)>();
            foreach (var template in EnumerateAircraftTemplates(world, out _))
            {
                if (!template.Has<XenonautAircraftType>())
                    continue;
                var type = template.Get<XenonautAircraftType>().value ?? "";
                if (string.Equals(type, typeQuery, StringComparison.OrdinalIgnoreCase))
                {
                    exact = template;
                    exactType = type;
                    break;
                }
                if (type.IndexOf(typeQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add((template, type));
            }
            if (exact != null)
            {
                matched = exactType;
                return exact;
            }
            if (partial.Count == 1)
            {
                matched = partial[0].Item2;
                return partial[0].Item1;
            }
            foreach (var (_, type) in partial)
                ambiguous.Add(type);
            return null;
        }

        public static List<Entity> EnumerateAliveGeoBases(World world)
        {
            var sys = world.GetSystem<GeoBaseSystem>();
            if (sys == null)
                return new List<Entity>();
            return sys.GetAliveGeoBases().ToList();
        }

        // Finds an empty enabled hangar in the given base, or returns null.
        public static Entity? FindEmptyHangarInGeoBase(World world, Entity geoBase)
        {
            var family = world.RegisterFamily(StrategyArchetypes.EmptyEnabledHangarOnSite);
            foreach (Entity hangar in family)
            {
                if (hangar.IsLinkedToBuildSite(geoBase))
                    return hangar;
            }
            return null;
        }

        // Picks (geoBase, hangar) for spawning. If geoBaseQuery is null, scans every alive
        // base; if it's set, matches by Name (exact or substring) and only considers that
        // base. Returns null reason on success.
        public static bool TryPickHangarForSpawn(
            World world,
            string? geoBaseQuery,
            out Entity? geoBase,
            out Entity? hangar,
            out string reason
        )
        {
            geoBase = null;
            hangar = null;
            reason = "";
            var bases = EnumerateAliveGeoBases(world);
            if (bases.Count == 0)
            {
                reason = "no alive geo bases";
                return false;
            }
            if (geoBaseQuery != null)
            {
                var matches = FindByName(bases, geoBaseQuery);
                if (matches.Count == 0)
                {
                    reason = $"no base matching '{geoBaseQuery}'";
                    return false;
                }
                if (matches.Count > 1)
                {
                    reason =
                        $"'{geoBaseQuery}' is ambiguous ({matches.Count} matches: "
                        + string.Join(", ", matches.Select(b => b.HasName() ? b.Name().value : "?"))
                        + ")";
                    return false;
                }
                geoBase = matches[0];
                hangar = FindEmptyHangarInGeoBase(world, geoBase);
                if (hangar == null)
                {
                    reason =
                        $"base '{(geoBase.HasName() ? geoBase.Name().value : "?")}' has no empty hangar";
                    return false;
                }
                return true;
            }
            foreach (var b in bases)
            {
                var h = FindEmptyHangarInGeoBase(world, b);
                if (h != null)
                {
                    geoBase = b;
                    hangar = h;
                    return true;
                }
            }
            reason = "no base with an empty hangar";
            return false;
        }

        // Spawns an aircraft of the given type into a hangar. Bypasses cost
        // and research/unlock gating. Returns the new aircraft entity.
        public static bool TrySpawnXenonautAircraft(
            World world,
            string typeQuery,
            string? geoBaseQuery,
            out Entity? aircraft,
            out string? matchedType,
            out string? matchedBase,
            out string reason
        )
        {
            aircraft = null;
            matchedType = null;
            matchedBase = null;
            reason = "";

            var template = FindAircraftTemplateByType(
                world,
                typeQuery,
                out matchedType,
                out var ambiguous
            );
            if (template == null)
            {
                if (ambiguous.Count > 0)
                    reason =
                        $"'{typeQuery}' is ambiguous ({ambiguous.Count} matches: "
                        + string.Join(", ", ambiguous)
                        + ")";
                else
                    reason = $"no aircraft type matching '{typeQuery}'";
                return false;
            }

            if (
                !TryPickHangarForSpawn(
                    world,
                    geoBaseQuery,
                    out var geoBase,
                    out var hangar,
                    out reason
                )
            )
                return false;
            matchedBase = geoBase!.HasName() ? geoBase.Name().value : null;

            if (!TryGetPlayer(world, out var player))
            {
                reason = "could not find player entity";
                return false;
            }

            var aircraftSystem = world.GetSystem<XenonautAircraftSystem>();
            if (aircraftSystem == null)
            {
                reason = "XenonautAircraftSystem missing";
                return false;
            }

            var spawned = aircraftSystem.GenerateAircraft(player, template);
            if (spawned == null)
            {
                reason = "GenerateAircraft returned null";
                return false;
            }

            // Bypass research/unlock gating in case the template is locked. Aircraft default
            // to Unlocked but a research-gated template stores Locked.
            if (
                spawned.HasUnlockedStateMachine()
                && spawned
                    .UnlockedStateMachine()
                    .IsInState(UnlockedStateMachineComponent.States.Locked)
                && spawned.CanTriggerUnlockedStateMachine(
                    UnlockedStateMachineComponent.Triggers.Unlock
                )
            )
            {
                spawned.TriggerUnlockedStateMachine(UnlockedStateMachineComponent.Triggers.Unlock);
            }

            aircraftSystem.RequisitionAircraft(
                player,
                hangar!,
                spawned,
                ignoreCost: true,
                delayed: false
            );

            aircraft = spawned;
            return true;
        }

        // Drives every in-progress project task of the given type to max
        // progress on the next DeltaTimeEvent. The CheckProjectFinished
        // subscriber then transitions each task through Finished, which is
        // the game's normal completion path: items award, downstreams
        // unlock, the row vanishes from the queue UI, refund accounting is
        // skipped (cost was already paid when the task started).
        //
        // Mirrors the game's internal ProjectSystem.CompleteCurrentProjects
        // dev cheat, narrowed to a single ProjectType. The same null-target
        // crash that path normally hits is suppressed by
        // Patches/ProjectQueueRowControllerPatch.
        public static List<string> CompleteInProgressProjects(World world, ProjectType type)
        {
            var snapshot = new List<(Entity Task, Entity Project, string Filename)>();
            var family = world.RegisterFamily(StrategyArchetypes.ProjectTask);
            foreach (Entity task in family)
            {
                if (!task.HasProject())
                    continue;
                Entity project = task.Project().Value;
                if (!project.ProjectType().Is(type))
                    continue;
                if (!task.IsInProjectStateMachine(ProjectStateMachineComponent.States.InProgress))
                    continue;
                snapshot.Add((task, project, StripAbstract(GetProjectFilenameOrFallback(project))));
            }
            var completed = new List<string>();
            foreach (var item in snapshot)
            {
                completed.Add(item.Filename);
                var task = item.Task;
                var project = item.Project;
                world.QueueTask(() =>
                {
                    if (
                        task.IsAlive()
                        && task.HasProject()
                        && task.Project().Value == project
                        && task.IsInProjectStateMachine(
                            ProjectStateMachineComponent.States.InProgress
                        )
                    )
                    {
                        task.ProgressPointsToMaximum();
                    }
                });
            }
            return completed;
        }

        // Template filename without extension (eg. "colossus_armour_upgrade").
        public static string GetProjectFilename(Entity project)
        {
            var path = project.TemplateReference()?.value?.GetAssetDescriptor()?.GetRelativePath();
            return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
        }

        private const string AbstractPrefix = "abstract_";

        private static string StripAbstract(string filename) =>
            filename != null
            && filename.StartsWith(AbstractPrefix, StringComparison.OrdinalIgnoreCase)
                ? filename.Substring(AbstractPrefix.Length)
                : filename;

        // Full template path (eg. "projects/research/colossus_armour_upgrade.json"). Used when
        // two projects share a filename (would only happen via a mod), then this string is the
        // unique identifier.
        public static string GetProjectRelativePath(Entity project)
        {
            return project.TemplateReference()?.value?.GetAssetDescriptor()?.GetRelativePath() ?? "";
        }

        // Friendly label for messages: "filename (Display Name)" when both are available,
        // otherwise whichever is non-empty. Strips the abstract_ prefix from the filename.
        public static string GetProjectLabel(Entity project)
        {
            var filename = StripAbstract(GetProjectFilename(project));
            var display = project.HasName() ? project.Name().value : null;
            if (!string.IsNullOrEmpty(filename) && !string.IsNullOrWhiteSpace(display))
                return $"{filename} ({display})";
            if (!string.IsNullOrEmpty(filename))
                return filename;
            if (!string.IsNullOrWhiteSpace(display))
                return display;
            return project.ToString();
        }

        // Best stable identifier we can report. Prefers filename; falls back to display name
        // for projects without an asset descriptor (shouldn't happen for real game data).
        private static string GetProjectFilenameOrFallback(Entity project)
        {
            var filename = GetProjectFilename(project);
            if (!string.IsNullOrEmpty(filename))
                return filename;
            return project.HasName() ? project.Name().value : project.ToString();
        }

        // Match query against the template filename (case-insensitive, with or without the
        // "abstract_" prefix). Exact filename match beats display-name matches; substring
        // filename match beats display-name substring. Display-name matching is kept as a
        // fallback so users can still type "Colossus" and hit both projects.
        public static List<Entity> FindProjectsByName(World world, ProjectType type, string query)
        {
            var family = world.RegisterFamily(StrategyArchetypes.Project);
            var exactFilename = new List<Entity>();
            var exactDisplay = new List<Entity>();
            var partialFilename = new List<Entity>();
            var partialDisplay = new List<Entity>();
            foreach (Entity project in family)
            {
                if (!project.ProjectType().Is(type))
                    continue;
                var filename = GetProjectFilename(project);
                var stripped = StripAbstract(filename);
                var display = project.HasName() ? project.Name().value : "";
                if (string.IsNullOrEmpty(filename) && string.IsNullOrWhiteSpace(display))
                    continue;
                bool exactFn = !string.IsNullOrEmpty(filename) && (
                    string.Equals(filename, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stripped, query, StringComparison.OrdinalIgnoreCase)
                );
                bool partialFn = !exactFn && !string.IsNullOrEmpty(filename) && (
                    filename.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || stripped.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                );
                if (exactFn)
                    exactFilename.Add(project);
                else if (
                    !string.IsNullOrWhiteSpace(display)
                    && string.Equals(display, query, StringComparison.OrdinalIgnoreCase)
                )
                    exactDisplay.Add(project);
                else if (partialFn)
                    partialFilename.Add(project);
                else if (
                    !string.IsNullOrWhiteSpace(display)
                    && display.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    partialDisplay.Add(project);
            }
            if (exactFilename.Count > 0)
                return exactFilename;
            if (exactDisplay.Count > 0)
                return exactDisplay;
            if (partialFilename.Count > 0)
                return partialFilename;
            return partialDisplay;
        }

        // Every project of the given type that isn't already finished, that has a display name
        // and that can be safely shortcut-completed.
        // Skip projects with FinishBuildAircraftEffect, which expects a Constructing link set
        // up by the InProgress entry effect, crashes the game.
        public static List<Entity> EnumerateUnfinishedProjects(
            World world,
            ProjectType type,
            out int skippedAircraftBuilds
        )
        {
            skippedAircraftBuilds = 0;
            var family = world.RegisterFamily(StrategyArchetypes.Project);
            var result = new List<Entity>();
            foreach (Entity project in family)
            {
                if (!project.ProjectType().Is(type) || IsProjectAlreadyDone(project))
                    continue;
                if (string.IsNullOrEmpty(GetProjectFilename(project)))
                    continue;
                if (!project.HasName() || string.IsNullOrWhiteSpace(project.Name().value))
                    continue;
                if (ProjectNeedsInProgressSetup(project))
                {
                    skippedAircraftBuilds++;
                    continue;
                }
                result.Add(project);
            }
            return result;
        }

        // Some projects (afaik currently only aircraft engineering projects) carry effects that
        // expect state set up by an earlier transition (eg. FinishBuildAircraftEffect reads the
        // Constructing link wired by StartBuildAircraftEffect). Forcing them straight to
        // Finished crashes.
        public static bool ProjectNeedsInProgressSetup(Entity project)
        {
            if (!project.HasProjectStateMachine())
                return false;
            foreach (var effect in project.ProjectStateMachine().AllEffects())
            {
                if (effect is FinishBuildAircraftEffect || effect is StartBuildAircraftEffect)
                    return true;
            }
            return false;
        }

        private static bool IsProjectAlreadyDone(Entity project)
        {
            if (project.ProjectMeta().Has(ProjectMetaComponent.Type.Repeatable))
                return false;
            if (!project.HasProjectTasks())
                return false;
            foreach (Entity task in project.ProjectTasks())
            {
                if (task.IsInProjectStateMachine(ProjectStateMachineComponent.States.Finished))
                    return true;
            }
            return false;
        }

        // Queues a project completion on the next DeltaTimeEvent.
        public static string FinishProject(World world, Entity project, Entity geoBase)
        {
            var filename = StripAbstract(GetProjectFilenameOrFallback(project));
            world.QueueTask(() =>
            {
                Entity task = Template
                    .CreateProjectTask(project)
                    .Add(new ProgressPoints(1f, 1f, 1f))
                    .Add(project.ProjectStateMachine().UnsafeCopy())
                    .Create(world);
                MyOneSelector.Set<MyGeoBaseSelector>(
                    task.ProjectStateMachine().AllEffects(),
                    geoBase
                );
                task.AddTasks(
                    Effects.TriggerProjectStateMachine(
                        ProjectStateMachineComponent.Triggers.GoTo,
                        ProjectStateMachineComponent.States.Finished
                    )
                );
                MyOneSelector.Set<MyGeoBaseSelector>(task.ProjectStateMachine().AllEffects(), null);
            });
            return filename;
        }

        public static Entity? PickAnyAliveGeoBase(World world)
        {
            return EnumerateAliveGeoBases(world).FirstOrDefault();
        }

        public class ProjectListing
        {
            public List<string> InProgress { get; } = new List<string>();
            public List<string> Available { get; } = new List<string>();
            public List<string> Locked { get; } = new List<string>();
            public List<string> Finished { get; } = new List<string>();
            public int Total => InProgress.Count + Available.Count + Locked.Count + Finished.Count;
        }

        public static ProjectListing ListProjects(World world, ProjectType type)
        {
            var bucketed =
                new List<(Entity Project, string Filename, string Path, ProjectStatus Status)>();
            var family = world.RegisterFamily(StrategyArchetypes.Project);
            foreach (Entity project in family)
            {
                if (!project.ProjectType().Is(type))
                    continue;
                var filename = GetProjectFilename(project);
                if (string.IsNullOrEmpty(filename))
                    continue;
                if (!project.HasName() || string.IsNullOrWhiteSpace(project.Name().value))
                    continue;

                bool hasInProgress = false;
                bool hasFinished = false;
                if (project.HasProjectTasks())
                {
                    foreach (Entity task in project.ProjectTasks())
                    {
                        if (
                            task.IsInProjectStateMachine(
                                ProjectStateMachineComponent.States.InProgress
                            )
                        )
                            hasInProgress = true;
                        else if (
                            task.IsInProjectStateMachine(
                                ProjectStateMachineComponent.States.Finished
                            )
                        )
                            hasFinished = true;
                    }
                }
                bool repeatable = project.ProjectMeta().Has(ProjectMetaComponent.Type.Repeatable);
                bool unlocked = project
                    .UnlockedStateMachine()
                    .IsInState(UnlockedStateMachineComponent.States.Unlocked);

                ProjectStatus status;
                if (hasInProgress)
                    status = ProjectStatus.InProgress;
                else if (hasFinished && !repeatable)
                    status = ProjectStatus.Finished;
                else if (unlocked)
                    status = ProjectStatus.Available;
                else
                    status = ProjectStatus.Locked;

                bucketed.Add((project, filename, GetProjectRelativePath(project), status));
            }

            // When two projects share a filename, fall back to the full relative path.
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in bucketed)
                counts[b.Filename] = counts.TryGetValue(b.Filename, out var c) ? c + 1 : 1;

            var listing = new ProjectListing();
            foreach (var b in bucketed)
            {
                var id = counts[b.Filename] > 1 ? b.Path : StripAbstract(b.Filename);
                var display = b.Project.Name().value;
                var entry = $"{id} ({display})";
                switch (b.Status)
                {
                    case ProjectStatus.InProgress:
                        listing.InProgress.Add(entry);
                        break;
                    case ProjectStatus.Available:
                        listing.Available.Add(entry);
                        break;
                    case ProjectStatus.Locked:
                        listing.Locked.Add(entry);
                        break;
                    case ProjectStatus.Finished:
                        listing.Finished.Add(entry);
                        break;
                }
            }

            listing.InProgress.Sort(StringComparer.OrdinalIgnoreCase);
            listing.Available.Sort(StringComparer.OrdinalIgnoreCase);
            listing.Locked.Sort(StringComparer.OrdinalIgnoreCase);
            listing.Finished.Sort(StringComparer.OrdinalIgnoreCase);
            return listing;
        }

        private enum ProjectStatus
        {
            InProgress,
            Available,
            Locked,
            Finished,
        }
    }
}
