using System;
using System.Collections.Generic;
using UnityEngine;

namespace GNOMES.Utilities
{
    // ── Spawn options ─────────────────────────────────────────────────────────

    public struct PoolSpawnOptions
    {
        public GameObject Prefab;
        public bool       SetPosition;
        public Vector3    Position;
        public bool       UseWorldSpace;
        public Transform  Parent;
    }

    // ── Per-prefab pool config ────────────────────────────────────────────────

    /// <summary>
    /// Optional config you can pass to <see cref="Pooler.RegisterPool"/> to
    /// control pre-warm size and growth rate for a specific prefab.
    /// If not registered explicitly, sensible defaults are used.
    /// </summary>
    public struct PoolConfig
    {
        /// <summary>How many instances to create up-front.</summary>
        public int InitialSize;

        /// <summary>How many extra instances to create when the pool runs dry.</summary>
        public int GrowthAmount;

        public static PoolConfig Default => new() { InitialSize = 4, GrowthAmount = 4 };
    }

    // ── Runtime pool ─────────────────────────────────────────────────────────

    [Serializable]
    internal class Pool
    {
        // The prefab this pool manages — never activated/deactivated directly.
        public GameObject       Prefab;
        public PoolConfig       Config;
        public Transform        Container;    // scene parent for pooled instances
        public List<GameObject> Objects = new();

        /// <summary>
        /// Returns the first inactive object, or null if none are available.
        /// Does NOT grow the pool — call <see cref="Grow"/> first if needed.
        /// </summary>
        public GameObject GetInactive() =>
            Objects.Find(o => o != null && !o.activeSelf);

        /// <summary>
        /// Instantiates <paramref name="count"/> new instances and parks them
        /// inactive under <see cref="Container"/>.
        /// </summary>
        public void Grow(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = UnityEngine.Object.Instantiate(Prefab, Container);
                instance.name = Prefab.name;     // keep names clean (no "(Clone)")
                instance.SetActive(false);
                Objects.Add(instance);
            }
        }
    }

    // ── Pooler ────────────────────────────────────────────────────────────────

    public class Pooler : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        private static Pooler _instance;

        private static Pooler Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // Auto-create if missing — but log a warning so it doesn't go unnoticed
                var go = new GameObject("[Pooler]");
                _instance = go.AddComponent<Pooler>();
                DontDestroyOnLoad(go);
                Debug.LogWarning(
                    "[Gnomes] Pooler was created automatically at runtime. " +
                    "Consider adding one to your scene manually for better control.");
                return _instance;
            }
        }

        // ── Internal state ────────────────────────────────────────────────────

        // Keyed by prefab instance ID so two prefabs with the same name never collide.
        private readonly Dictionary<int, Pool> _pools = new();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Pre-warms a pool for <paramref name="prefab"/> with the given config.
        /// Call this during a loading screen to avoid first-spawn hitches.
        /// Safe to call multiple times — subsequent calls are ignored.
        /// </summary>
        public static void RegisterPool(GameObject prefab, PoolConfig config)
        {
            if (prefab == null)
            {
                Debug.LogError("[Gnomes] Pooler.RegisterPool called with a null prefab.");
                return;
            }
            Instance.GetOrCreatePool(prefab, config);
        }

        /// <summary>Spawns a pooled instance of <paramref name="prefab"/>.</summary>
        public static GameObject Spawn(
            GameObject prefab,
            Transform  parent        = null,
            bool       useWorldSpace = false)
        {
            if (prefab == null)
            {
                Debug.LogError("[Gnomes] Pooler.Spawn called with a null prefab.");
                return null;
            }

            var options = new PoolSpawnOptions
            {
                Prefab        = prefab,
                SetPosition   = false,
                UseWorldSpace = useWorldSpace,
                Parent        = parent
            };
            return Instance.SpawnInternal(options);
        }

        /// <summary>Spawns a pooled instance at a specific world position.</summary>
        public static GameObject SpawnAt(
            GameObject prefab,
            Vector3    position,
            Transform  parent        = null,
            bool       useWorldSpace = false)
        {
            if (prefab == null)
            {
                Debug.LogError("[Gnomes] Pooler.SpawnAt called with a null prefab.");
                return null;
            }

            var options = new PoolSpawnOptions
            {
                Prefab        = prefab,
                SetPosition   = true,
                Position      = position,
                UseWorldSpace = useWorldSpace,
                Parent        = parent
            };
            return Instance.SpawnInternal(options);
        }

        /// <summary>
        /// Returns <paramref name="instance"/> to its pool (sets it inactive and
        /// re-parents it under the pool container).
        /// Safe to call on objects not managed by this pooler — they are simply
        /// deactivated and a warning is logged.
        /// </summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;

            // Find the pool that owns this instance
            foreach (var pool in Instance._pools.Values)
            {
                if (!pool.Objects.Contains(instance)) continue;
                instance.SetActive(false);
                instance.transform.SetParent(pool.Container);
                return;
            }

            // Not from a pool — deactivate and warn rather than silently doing nothing
            instance.SetActive(false);
            Debug.LogWarning(
                $"[Gnomes] Pooler.Despawn called on '{instance.name}' which is not " +
                "managed by this pooler. Object has been deactivated.");
        }

        /// <summary>
        /// Deactivates all active instances across every pool — useful for
        /// level resets or returning everything at once.
        /// </summary>
        public static void DespawnAll()
        {
            foreach (var pool in Instance._pools.Values)
                foreach (var obj in pool.Objects)
                    if (obj != null && obj.activeSelf)
                        obj.SetActive(false);
        }

        /// <summary>
        /// Destroys all instances in the pool for <paramref name="prefab"/> and
        /// removes the pool. Useful when unloading a scene's specific content.
        /// </summary>
        public static void DestroyPool(GameObject prefab)
        {
            if (prefab == null) return;

            int id = prefab.GetInstanceID();
            if (!Instance._pools.TryGetValue(id, out var pool)) return;

            foreach (var obj in pool.Objects)
                if (obj != null) Destroy(obj);

            if (pool.Container != null)
                Destroy(pool.Container.gameObject);

            Instance._pools.Remove(id);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private GameObject SpawnInternal(PoolSpawnOptions options)
        {
            var pool = GetOrCreatePool(options.Prefab, PoolConfig.Default);

            // Find an available inactive instance; grow the pool if exhausted
            var obj = pool.GetInactive();
            if (obj == null)
            {
                pool.Grow(pool.Config.GrowthAmount);
                obj = pool.GetInactive();

                if (obj == null)
                {
                    // Should never happen after Grow, but fail loudly rather than silently
                    Debug.LogError(
                        $"[Gnomes] Pool for '{options.Prefab.name}' failed to produce " +
                        "an instance after growing. This is a bug — please report it.");
                    return null;
                }
            }

            // Set position if requested
            if (options.SetPosition)
                obj.transform.position = options.Position;

            if (options.Parent != null)
            {
                // If a parent is specified (like an inventory slot or an attachment node), use it
                obj.transform.SetParent(options.Parent, options.UseWorldSpace);
            }
            else
            {
                // If no parent is given, strip the DDOL parent container out!
                obj.transform.SetParent(null, options.UseWorldSpace);
        
                // Push it explicitly into the scene currently being played
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, activeScene);
            }

            obj.SetActive(true);
            return obj;
        }

        private Pool GetOrCreatePool(GameObject prefab, PoolConfig config)
        {
            int id = prefab.GetInstanceID();
            if (_pools.TryGetValue(id, out var existing)) return existing;

            // Create a tidy container object under the Pooler
            var containerGo = new GameObject(prefab.name);
            containerGo.transform.SetParent(transform);

            var pool = new Pool
            {
                Prefab    = prefab,
                Config    = config,
                Container = containerGo.transform
            };

            // Pre-warm
            pool.Grow(config.InitialSize);

            _pools[id] = pool;
            return pool;
        }
    }
}