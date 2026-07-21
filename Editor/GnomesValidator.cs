// ═════════════════════════════════════════════════════════════════════════════
//  GnomesValidator.cs — Editor only
//
//  Runs automatically on every domain reload (play mode enter, script compile,
//  prefab save) and validates that every Actor in the project is correctly
//  configured. Catches missing modules, missing [SerializeReference],
//  and behaviour ordering conflicts before they become runtime mysteries.
//
//  All warnings are logged with the Actor as context so clicking them in
//  the Console pings the offending object in the Hierarchy or Project window.
// ═════════════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Reflection;
using GNOMES.Actor.Core;
using GNOMES.Runtime;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

namespace GNOMES.Editor
{
    [InitializeOnLoad]
    internal static class GnomesValidator
    {
        // ── Entry point ───────────────────────────────────────────────────────

        static GnomesValidator()
        {
            // Defer until the editor is fully loaded so asset databases and
            // prefab systems are ready. Running too early produces false positives.
            EditorApplication.delayCall += RunAll;
        }

        private static void RunAll()
        {
            ValidateGnomeableFields();
            ValidateActors();
        }

        // ── [Gnomeable] field validation ──────────────────────────────────────

        /// <summary>
        /// Checks every MonoBehaviour and ScriptableObject in loaded assemblies
        /// for [Gnomeable] fields missing [SerializeReference].
        /// </summary>
        private static void ValidateGnomeableFields()
        {
            var targets = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t =>
                    !t.IsAbstract &&
                    (typeof(MonoBehaviour).IsAssignableFrom(t) ||
                     typeof(ScriptableObject).IsAssignableFrom(t)));

            foreach (var type in targets)
            {
                foreach (var field in type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public   |
                    BindingFlags.NonPublic))
                {
                    if (field.GetCustomAttribute<GnomeableAttribute>() == null)
                        continue;

                    if (field.GetCustomAttribute<SerializeReference>() == null)
                        Debug.LogWarning(
                            $"[Gnomes] <b>{type.Name}.{field.Name}</b> has " +
                            $"[Gnomeable] but is missing <b>[SerializeReference]</b>. " +
                            $"Polymorphic serialization will not work without it.\n" +
                            $"Fix: add [SerializeReference] above [Gnomeable].");
                }
            }
        }

        // ── Actor validation ──────────────────────────────────────────────────

        /// <summary>
        /// Validates every Actor found in loaded prefabs and open scenes.
        /// </summary>
        private static void ValidateActors()
        {
            // Prefabs
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                foreach (var actor in prefab.GetComponentsInChildren<Actor.Core.Actor>(true))
                    ValidateActor(actor);
            }

            // Open scenes
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                foreach (var actor in root.GetComponentsInChildren<Actor.Core.Actor>(true))
                    ValidateActor(actor);
            }
        }

        private static void ValidateActor(Actor.Core.Actor actor)
        {
            if (actor == null) return;

            ValidateBrainModules(actor);
            ValidateBehaviourOrdering(actor);
        }

        // ── Module requirement validation ─────────────────────────────────────

        /// <summary>
        /// Checks that every [RequiresModule] declared by InitialBehaviors is
        /// satisfied by the DefaultBrain's module list.
        /// </summary>
        private static void ValidateBrainModules(Actor.Core.Actor actor)
        {
            // Reflect to get DefaultBrain — it's private/serialized
            var brainField = typeof(Actor.Core.Actor).GetField(
                "DefaultBrain",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var brain = brainField?.GetValue(actor) as ActorBrain;

            foreach (var behavior in actor.InitialBehaviors)
            {
                if (behavior == null) continue;

                var requirements = behavior.GetType()
                    .GetCustomAttributes<RequiresModuleAttribute>(inherit: true);

                foreach (var req in requirements)
                {
                    // No brain — can't validate module presence
                    if (brain == null)
                    {
                        Debug.LogWarning(
                            $"[Gnomes] <b>{actor.name}</b> has " +
                            $"<b>{behavior.GetType().Name}</b> which requires " +
                            $"<b>{req.ModuleType.Name}</b>, but no DefaultBrain " +
                            $"is assigned.",
                            actor);
                        continue;
                    }

                    // Check if modules are validated OR if the brain is a empty default brain.
                    bool satisfied = brain.Modules
                        .Any(m => m != null &&
                                  req.ModuleType.IsInstanceOfType(m)) || brain.GetType() == typeof(ActorBrain);

                    if (!satisfied)
                        Debug.LogWarning(
                            $"[Gnomes] <b>{actor.name}</b> → " +
                            $"<b>{behavior.GetType().Name}</b> requires " +
                            $"<b>{req.ModuleType.Name}</b>, but brain " +
                            $"'<b>{brain.name}</b>' has no such module.\n" +
                            $"Fix: select the brain asset and add " +
                            $"{req.ModuleType.Name} via the inspector.",
                            actor);
                }
            }
        }

        // ── Behaviour ordering validation ─────────────────────────────────────

        /// <summary>
        /// Warns when two behaviours in InitialBehaviors write to the same
        /// module observable and have the same execution order — the last-write
        /// winner is undefined and likely a bug.
        /// </summary>
        private static void ValidateBehaviourOrdering(Actor.Core.Actor actor)
        {
            // Build a map of (moduleType, fieldName) → list of behaviours that
            // declare they write to it, grouped by BehaviourOrder.
            // We detect this via [WritesTo] if present — if not, we skip since
            // we can't reflect into lambda closures.

            // For now: warn if two behaviours share the same BehaviourOrder
            // AND both require the same module (a proxy for likely conflict).
            var behaviors = actor.InitialBehaviors
                .Where(b => b != null)
                .ToList();

            // Group by order value
            var byOrder = behaviors
                .GroupBy(b =>
                {
                    var attr = b.GetType()
                        .GetCustomAttribute<BehaviourOrderAttribute>(true);
                    return attr?.Order ?? 0;
                });

            foreach (var orderGroup in byOrder)
            {
                if (orderGroup.Count() < 2) continue;

                // Find behaviours in the same order slot that share a module requirement
                var list = orderGroup.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var reqsA = list[i].GetType()
                            .GetCustomAttributes<RequiresModuleAttribute>(true)
                            .Select(r => r.ModuleType)
                            .ToHashSet();

                        var reqsB = list[j].GetType()
                            .GetCustomAttributes<RequiresModuleAttribute>(true)
                            .Select(r => r.ModuleType)
                            .ToHashSet();

                        var shared = reqsA.Intersect(reqsB).ToList();
                        if (shared.Count == 0) continue;

                        Debug.LogWarning(
                            $"[Gnomes] <b>{actor.name}</b>: " +
                            $"<b>{list[i].GetType().Name}</b> and " +
                            $"<b>{list[j].GetType().Name}</b> both run at " +
                            $"BehaviourOrder({orderGroup.Key}) and share module(s): " +
                            $"{string.Join(", ", shared.Select(t => t.Name))}.\n" +
                            $"If both write to the same module fields, the last " +
                            $"write wins non-deterministically. Consider adding " +
                            $"[BehaviourOrder] to separate them.",
                            actor);
                    }
                }
            }
        }

        // ── Manual trigger ────────────────────────────────────────────────────

        [MenuItem("Tools/Gnomes/Run Validator")]
        private static void RunManually()
        {
            Debug.Log("[Gnomes] Running validator...");
            RunAll();
            Debug.Log("[Gnomes] Validation complete. Check Console for warnings.");
        }
    }
}

#endif