using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GNOMES.Actor.Serialization {
    /// <summary>
    /// Orchestrates save and load for all GnomesIdentity actors in the scene.
    ///
    /// Usage:
    ///   // Save
    ///   string json = GnomesSaveSystem.Export();
    ///   File.WriteAllText(savePath, json);   // or PlayerPrefs, cloud, etc.
    ///
    ///   // Load
    ///   string json = File.ReadAllText(savePath);
    ///   GnomesSaveSystem.Import(json);
    ///
    /// Only modules that implement ISaveable are included in the payload.
    /// Modules that don't implement ISaveable are ignored entirely.
    /// </summary>
    public static class GnomesSaveSystem
    {
        // ── Export ────────────────────────────────────────────────────────────

        /// <summary>
        /// Collects state from all GnomesIdentity actors in the active scenes
        /// and returns it as a JSON string. Pass this string to your own
        /// storage system — file, PlayerPrefs, cloud, etc.
        /// </summary>
        public static string Export()
        {
            var payload = new GnomesSavePayload();

            foreach (var identity in FindAllIdentities())
            {
                var actor = identity.GetComponent<Core.Actor>();
                if (actor == null) continue;

                var actorSave = new ActorSave
                {
                    guid       = identity.Guid,
                    prefabPath = identity.PrefabPath,
                    position   = Vec3ToArray(identity.transform.position),
                    rotation   = QuatToArray(identity.transform.rotation)
                };

                // Collect ISaveable modules from the active brain
                if (actor.Brain != null)
                {
                    foreach (var module in actor.Brain.Modules)
                    {
                        if (module is not ISaveable saveable) continue;

                        try
                        {
                            var data     = saveable.Save();
                            string dataJson = SerializeDictionary(data);

                            actorSave.modules.Add(new ModuleSave
                            {
                                moduleTypeName = module.GetType().FullName,
                                dataJson       = dataJson
                            });
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(
                                $"[Gnomes] Save failed for module " +
                                $"{module.GetType().Name} on '{identity.name}': " +
                                $"{e.Message}");
                        }
                    }
                }

                payload.actors.Add(actorSave);
            }

            string json = JsonUtility.ToJson(payload, prettyPrint: true);
            Debug.Log($"[Gnomes] Exported {payload.actors.Count} actor(s).");
            return json;
        }

        // ── Import ────────────────────────────────────────────────────────────

        /// <summary>
        /// Restores actor state from a JSON string previously produced by Export().
        ///
        /// Scene actors are found by GUID and their module state is restored.
        /// Spawned actors (prefabPath != null) are respawned first, given their
        /// saved GUID, positioned, then have their module state restored.
        ///
        /// Call this after your scene is loaded and all actors are initialized.
        /// </summary>
        public static void Import(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[Gnomes] Import called with empty JSON.");
                return;
            }

            GnomesSavePayload payload;
            try
            {
                payload = JsonUtility.FromJson<GnomesSavePayload>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gnomes] Failed to parse save data: {e.Message}");
                return;
            }

            // Build a lookup of existing scene identities
            var existing = FindAllIdentities()
                .ToDictionary(id => id.Guid);

            foreach (var actorSave in payload.actors)
            {
                try
                {
                    RestoreActor(actorSave, existing);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[Gnomes] Failed to restore actor '{actorSave.guid}': " +
                        $"{e.Message}");
                }
            }

            Debug.Log($"[Gnomes] Imported {payload.actors.Count} actor(s).");
        }

        private static void RestoreActor(
            ActorSave save,
            Dictionary<string, GnomesIdentity> existing)
        {
            GnomesIdentity identity;

            if (!string.IsNullOrEmpty(save.prefabPath))
            {
                // Spawned actor — respawn it first
                var prefab = UnityEngine.Resources.Load<GameObject>(save.prefabPath);
                if (prefab == null)
                {
                    // Try direct asset load for non-Resources prefabs
#if UNITY_EDITOR
                    prefab = UnityEditor.AssetDatabase
                        .LoadAssetAtPath<GameObject>(save.prefabPath);
#endif

                    if (prefab == null)
                    {
                        Debug.LogError(
                            $"[Gnomes] Cannot load prefab at '{save.prefabPath}' " +
                            $"for actor '{save.guid}'. Skipping.");
                        return;
                    }
                }

                var go = UnityEngine.Object.Instantiate(prefab,
                    ArrayToVec3(save.position),
                    ArrayToQuat(save.rotation));

                identity = go.GetComponent<GnomesIdentity>();
                if (identity == null)
                {
                    Debug.LogError(
                        $"[Gnomes] Spawned prefab '{save.prefabPath}' has no " +
                        $"GnomesIdentity component. Skipping.");
                    UnityEngine.Object.Destroy(go);
                    return;
                }

                // Restore the saved GUID so future saves can track it
                identity.RestoreGuid(save.guid);
                identity.PrefabPath = save.prefabPath;
            }
            else
            {
                // Scene actor — find by GUID
                if (!existing.TryGetValue(save.guid, out identity))
                {
                    Debug.LogWarning(
                        $"[Gnomes] Scene actor with GUID '{save.guid}' not found. " +
                        $"It may have been removed from the scene.");
                    return;
                }

                // Restore transform for scene actors too (they may have moved)
                identity.transform.position = ArrayToVec3(save.position);
                identity.transform.rotation = ArrayToQuat(save.rotation);
            }

            // Restore module state
            var actor = identity.GetComponent<Core.Actor>();
            if (actor?.Brain == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] Actor '{identity.name}' has no active brain — " +
                    $"module state not restored. Ensure the actor is initialized " +
                    $"before calling Import.");
                return;
            }

            RestoreModules(actor, save.modules);
        }

        private static void RestoreModules(Core.Actor actor, List<ModuleSave> moduleSaves)
        {
            foreach (var moduleSave in moduleSaves)
            {
                // Find the matching module by type name
                var module = actor.Brain.Modules
                    .FirstOrDefault(m =>
                        m != null &&
                        (m.GetType().FullName == moduleSave.moduleTypeName ||
                         m.GetType().Name     == moduleSave.moduleTypeName));

                if (module == null)
                {
                    Debug.LogWarning(
                        $"[Gnomes] Actor '{actor.name}': no module of type " +
                        $"'{moduleSave.moduleTypeName}' found on brain " +
                        $"'{actor.Brain.name}'. Skipping.");
                    continue;
                }

                if (module is not ISaveable saveable)
                {
                    Debug.LogWarning(
                        $"[Gnomes] Module '{moduleSave.moduleTypeName}' on " +
                        $"'{actor.name}' does not implement ISaveable. Skipping.");
                    continue;
                }

                try
                {
                    var data = DeserializeDictionary(moduleSave.dataJson);
                    saveable.Load(data);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[Gnomes] Load failed for module " +
                        $"'{moduleSave.moduleTypeName}' on '{actor.name}': " +
                        $"{e.Message}");
                }
            }
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static IEnumerable<GnomesIdentity> FindAllIdentities() =>
            UnityEngine.Object.FindObjectsByType<GnomesIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        // ── JSON dictionary serialization ─────────────────────────────────────
        // JsonUtility can't serialize Dictionary<string, object> directly.
        // We use a simple key-value list wrapper instead.

        [Serializable]
        private class JsonKVPair
        {
            public string key;
            public string valueJson;    // each value serialized independently
            public string valueType;    // type hint for deserialization
        }

        [Serializable]
        private class JsonKVList
        {
            public List<JsonKVPair> pairs = new();
        }

        private static string SerializeDictionary(Dictionary<string, object> dict)
        {
            var kvList = new JsonKVList();

            foreach (var (key, value) in dict)
            {
                string valueJson;
                string valueType;

                if (value == null)
                {
                    valueJson = "null";
                    valueType = "null";
                }
                else
                {
                    valueType = value.GetType().FullName;
                    valueJson = value switch
                    {
                        // Primitives
                        int    i => i.ToString(),
                        float  f => f.ToString("R"),
                        double d => d.ToString("R"),
                        bool   b => b.ToString(),
                        string s => JsonUtility.ToJson(new StringWrapper { v = s }),

                        // Unity structs
                        Vector2    v2  => JsonUtility.ToJson(v2),
                        Vector3    v3  => JsonUtility.ToJson(v3),
                        Vector4    v4  => JsonUtility.ToJson(v4),
                        Quaternion q   => JsonUtility.ToJson(q),
                        Color      c   => JsonUtility.ToJson(c),

                        // Anything else — try JsonUtility
                        _ => JsonUtility.ToJson(value)
                    };
                }

                kvList.pairs.Add(new JsonKVPair
                    { key = key, valueJson = valueJson, valueType = valueType });
            }

            return JsonUtility.ToJson(kvList);
        }

        private static Dictionary<string, object> DeserializeDictionary(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(json)) return result;

            var kvList = JsonUtility.FromJson<JsonKVList>(json);
            if (kvList?.pairs == null) return result;

            foreach (var pair in kvList.pairs)
            {
                object value = pair.valueType switch
                {
                    "null"                    => null,
                    "System.Int32"            => int.Parse(pair.valueJson),
                    "System.Single"           => float.Parse(pair.valueJson),
                    "System.Double"           => double.Parse(pair.valueJson),
                    "System.Boolean"          => bool.Parse(pair.valueJson),
                    "System.String"           => JsonUtility
                        .FromJson<StringWrapper>(pair.valueJson).v,
                    "UnityEngine.Vector2"     => JsonUtility
                        .FromJson<Vector2>(pair.valueJson),
                    "UnityEngine.Vector3"     => JsonUtility
                        .FromJson<Vector3>(pair.valueJson),
                    "UnityEngine.Vector4"     => JsonUtility
                        .FromJson<Vector4>(pair.valueJson),
                    "UnityEngine.Quaternion"  => JsonUtility
                        .FromJson<Quaternion>(pair.valueJson),
                    "UnityEngine.Color"       => JsonUtility
                        .FromJson<Color>(pair.valueJson),
                    _                         => pair.valueJson  // raw string fallback
                };

                result[pair.key] = value;
            }

            return result;
        }

        // JsonUtility can't serialize a bare string — wrap it
        [Serializable] private class StringWrapper { public string v; }

        // ── Vector / Quaternion helpers ───────────────────────────────────────

        private static float[] Vec3ToArray(Vector3 v)    => new[] { v.x, v.y, v.z };
        private static float[] QuatToArray(Quaternion q) => new[] { q.x, q.y, q.z, q.w };

        private static Vector3 ArrayToVec3(float[] a) =>
            a?.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

        private static Quaternion ArrayToQuat(float[] a) =>
            a?.Length >= 4 ? new Quaternion(a[0], a[1], a[2], a[3]) : Quaternion.identity;
    }
}