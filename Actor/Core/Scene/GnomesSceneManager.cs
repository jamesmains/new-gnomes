using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GNOMES.Actor.Serialization;
using GNOMES.Runtime;
using GNOMES.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GNOMES.Actor.Core.Scene {
    /// <summary>
    /// Owns async scene loading/unloading (single or additive), player
    /// lifecycle coordination across N local players, spawn point
    /// resolution, and transition event firing.
    ///
    /// Player actors are spawned and despawned via the Gnomes Pooler —
    /// never Instantiate/Destroy directly — so pooled state (and anything
    /// that depends on it, like the save system's GnomesIdentity) is
    /// respected consistently with the rest of the framework.
    /// </summary>
    public class GnomesSceneManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        private static GnomesSceneManager _instance;

        public static GnomesSceneManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[GnomesSceneManager]");
                _instance = go.AddComponent<GnomesSceneManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Defaults")]
        public PlayerPersistenceMode DefaultPersistenceMode = PlayerPersistenceMode.Respawn;

        [Tooltip("Prefab used to spawn players in Respawn mode if a slot " +
                 "doesn't specify its own ActorPrefab. Must have Actor + GnomesIdentity.")]
        public GameObject DefaultPlayerPrefab;

        // ── Player slots ──────────────────────────────────────────────────────
        
        // Keyed by PlayerIndex so multiple local players are tracked independently.
        private readonly Dictionary<Player, GnomesPlayerSlot> _playerSlots = new();
        
        public IReadOnlyCollection<GnomesPlayerSlot> PlayerSlots => _playerSlots.Values;

        public static bool HasPlayer(Player player) => Instance._playerSlots.ContainsKey(player);

        public static bool HasPlayer(int playerIndex) =>
            Instance._playerSlots.FirstOrDefault(o => o.Value.PlayerIndex == playerIndex).Value != null;
        // ── Events ────────────────────────────────────────────────────────────

        public static event Action<string> OnBeforeUnload;
        public static event Action<float>  OnLoadProgress;
        public static event Action         OnSceneReady;
        public static event Action         OnTransitionComplete;

        /// <summary>Fired after an additive scene finishes unloading via UnloadScene.</summary>
        public static event Action<string> OnSceneUnloaded;

        // ── State ─────────────────────────────────────────────────────────────

        private bool   _isTransitioning;
        private string _savedState;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Player registration ───────────────────────────────────────────────

        /// <summary>
        /// Registers a local player slot. Call once per player when they join
        /// (e.g. from your PlayerInputManager.onPlayerJoined handler) before
        /// the first scene transition that should manage their actor.
        /// </summary>
        public static GnomesPlayerSlot RegisterPlayer(
            int playerIndex, Player player, PlayerBrain brainAsset,
            GameObject actorPrefab = null)
        {
            var slot = new GnomesPlayerSlot
            {
                PlayerIndex = playerIndex,
                Player      = player,
                BrainAsset  = brainAsset,
                ActorPrefab = actorPrefab
            };
            Debug.Log($"[Gnomes Scene Manager] Registering player {slot.PlayerIndex}");
            Instance._playerSlots[player] = slot;
            return slot;
        }

        /// <summary>
        /// Associates an already-spawned Actor with a player slot.
        /// Call after spawning a player manually (e.g. on initial game start)
        /// so the scene manager can manage it across future transitions.
        /// </summary>
        public static void AssignActorToSlot(Player player, Actor actor)
        {
            if (Instance._playerSlots.TryGetValue(player, out var slot))
                slot.CurrentActor = actor;
            else
                Debug.LogWarning(
                    $"[Gnomes] AssignActorToSlot: no slot registered for " +
                    $"player {player.gameObject.name}. Call RegisterPlayer first.");
        }

        /// <summary>Removes a player slot — call when a player leaves the session entirely.</summary>
        public static void UnregisterPlayer(Player player)
        {
            if (Instance._playerSlots.TryGetValue(player, out var slot))
            {
                if (slot.CurrentActor != null)
                    Pooler.Despawn(slot.CurrentActor.gameObject);
                Instance._playerSlots.Remove(player);
            }
        }

        // ── Public API — single scene transition ──────────────────────────────

        /// <summary>
        /// Begins a full scene transition (unload current, load destination)
        /// with default config.
        /// </summary>
        public static void LoadScene(string sceneName) =>
            LoadScene(sceneName, GnomesTransitionConfig.Default);

        /// <summary>
        /// Begins a scene transition with a specific config. If
        /// config.Additive is true, the destination scene is loaded
        /// alongside the current one rather than replacing it.
        /// </summary>
        public static void LoadScene(string sceneName, GnomesTransitionConfig config)
        {
            if (Instance._isTransitioning)
            {
                Debug.LogWarning(
                    $"[Gnomes] GnomesSceneManager: transition already in " +
                    $"progress. Ignoring request to load '{sceneName}'.");
                return;
            }

            config ??= GnomesTransitionConfig.Default;
            Instance.StartCoroutine(
                Instance.TransitionRoutine(sceneName, config));
        }

        /// <summary>
        /// Unloads a single additively-loaded scene by name without touching
        /// any other loaded scenes or player actors. Use this for streaming
        /// or layered scene setups (e.g. unloading a finished dungeon wing
        /// while the hub scene stays active).
        /// </summary>
        public static void UnloadScene(string sceneName)
        {
            Instance.StartCoroutine(Instance.UnloadSceneRoutine(sceneName));
        }

        private IEnumerator UnloadSceneRoutine(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning(
                    $"[Gnomes] UnloadScene: '{sceneName}' is not currently loaded.");
                yield break;
            }

            var operation = SceneManager.UnloadSceneAsync(scene);
            while (operation != null && !operation.isDone)
                yield return null;

            OnSceneUnloaded?.Invoke(sceneName);
        }

        // ── Transition routine ────────────────────────────────────────────────

        private IEnumerator TransitionRoutine(
            string sceneName, GnomesTransitionConfig config)
        {
            _isTransitioning = true;

            // ── 1. Pre-unload ─────────────────────────────────────────────────
            OnBeforeUnload?.Invoke(sceneName);

            if (config.SaveBeforeLoad)
                _savedState = GnomesSaveSystem.Export();

            SetAllPlayersInputEnabled(false);

            // ── 2. Fade in transition canvas ──────────────────────────────────
            if (GnomesTransitionCanvas.Instance != null)
                yield return GnomesTransitionCanvas.Instance.FadeInRoutine();

            // ── 3. Handle player persistence (pre-unload) ─────────────────────
            // Skipped entirely for additive loads — the current scene and its
            // actors are untouched, we're only adding a new scene on top.
            if (!config.Additive)
                yield return HandlePlayersPreUnload(config);

            // ── 4. Async load ─────────────────────────────────────────────────
            var loadMode = config.Additive
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;

            var operation = SceneManager.LoadSceneAsync(sceneName, loadMode);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                OnLoadProgress?.Invoke(operation.progress);
                yield return null;
            }

            OnLoadProgress?.Invoke(1f);
            operation.allowSceneActivation = true;

            while (!operation.isDone)
                yield return null;

            // If additive, make the newly loaded scene active so spawned
            // objects and spawn point lookups resolve against it correctly.
            if (config.Additive)
            {
                var loadedScene = SceneManager.GetSceneByName(sceneName);
                if (loadedScene.IsValid())
                    SceneManager.SetActiveScene(loadedScene);
            }

            // ── 5. Handle player persistence (post-load) ───────────────────────
            if (!config.Additive)
                yield return HandlePlayersPostLoad(config);

            if (config.LoadAfterReady && !string.IsNullOrEmpty(_savedState))
                GnomesSaveSystem.Import(_savedState);

            OnSceneReady?.Invoke();
            yield return ReinitializeSystems();

            // ── 6. Fade out transition canvas ─────────────────────────────────
            if (GnomesTransitionCanvas.Instance != null)
                yield return GnomesTransitionCanvas.Instance.FadeOutRoutine();

            SetAllPlayersInputEnabled(true);

            OnTransitionComplete?.Invoke();
            _isTransitioning = false;
        }

        // ── Player lifecycle — all spawn/despawn goes through Pooler ──────────

        private IEnumerator HandlePlayersPreUnload(GnomesTransitionConfig config)
        {
            if (config.PersistenceMode == PlayerPersistenceMode.Manual)
                yield break;

            foreach (var slot in _playerSlots.Values)
            {
                if (slot.CurrentActor == null) continue;

                switch (config.PersistenceMode)
                {
                    case PlayerPersistenceMode.Persist:
                        // Survive the unload via DontDestroyOnLoad. Re-parented
                        // back into the active scene after load completes.
                        DontDestroyOnLoad(slot.CurrentActor.gameObject);
                        break;

                    case PlayerPersistenceMode.Respawn:
                        // Return to the pool rather than destroying — this is
                        // the fix from the previous pass. The pooled instance
                        // is deactivated and reused on the next Pooler.Spawn
                        // call for the same prefab.
                        Pooler.Despawn(slot.CurrentActor.gameObject);
                        slot.CurrentActor = null;
                        break;
                }
            }

            yield return null;
        }

        private IEnumerator HandlePlayersPostLoad(GnomesTransitionConfig config)
        {
            if (config.PersistenceMode == PlayerPersistenceMode.Manual)
                yield break;
            foreach (var slot in _playerSlots.Values)
            {
                Debug.Log($"[Scene Manager] Slot: {slot.PlayerIndex}, Player: {slot.Player}" +
                          $" Actor Prefab: {slot.ActorPrefab}, Brain: {slot.BrainAsset}");
                var spawnPoint = GnomesSpawnPoint.Find(
                    config.SpawnPointId, slot.PlayerIndex);

                switch (config.PersistenceMode)
                {
                    case PlayerPersistenceMode.Persist:
                        if (slot.CurrentActor != null)
                        {
                            if (spawnPoint != null)
                            {
                                slot.Player.gameObject.transform.position = slot.CurrentActor.transform.position = spawnPoint.transform.position;
                                slot.Player.gameObject.transform.rotation = slot.CurrentActor.transform.rotation = spawnPoint.transform.rotation;
                            }

                            SceneManager.MoveGameObjectToScene(
                                slot.CurrentActor.gameObject,
                                SceneManager.GetActiveScene());
                        }
                        break;

                    case PlayerPersistenceMode.Respawn:
                        yield return RespawnPlayerSlot(slot, spawnPoint, config);
                        break;
                }

                // Re-bind the actor's brain to this slot's Player + PlayerBrain
                // template now that the actor exists in the new scene. This is
                // the fix for "doesn't handle PlayerBrain to Player" — the
                // relationship is re-established explicitly per slot rather
                // than assumed.
                RebindSlotBrain(slot);
            }
        }

        /// <summary>
        /// Pool-spawns a fresh actor body for a player slot at the resolved
        /// spawn point. Uses Pooler.SpawnAt rather than Instantiate so the
        /// actor participates in the same pooling lifecycle as everything
        /// else spawned through Gnomes.
        /// </summary>
        private IEnumerator RespawnPlayerSlot(
            GnomesPlayerSlot slot,
            GnomesSpawnPoint spawnPoint,
            GnomesTransitionConfig config)
        {
            var prefab = config.PlayerPrefabOverride ? config.PlayerPrefabOverride : slot.ActorPrefab ? slot.ActorPrefab : DefaultPlayerPrefab;

            if (prefab == null)
            {
                Debug.LogError(
                    $"[Gnomes] GnomesSceneManager: no player prefab available " +
                    $"for player slot {slot.PlayerIndex}. Cannot respawn.");
                yield break;
            }

            Vector3 pos = spawnPoint?.transform.position ?? Vector3.zero;

            var go = Pooler.SpawnAt(prefab, pos);
            if (go == null)
            {
                Debug.LogError(
                    $"[Gnomes] GnomesSceneManager: Pooler.SpawnAt returned null " +
                    $"for player slot {slot.PlayerIndex}.");
                yield break;
            }

            if (spawnPoint != null)
                go.transform.rotation = spawnPoint.transform.rotation;

            var actor = go.GetComponent<Actor>();
            if (actor == null)
            {
                Debug.LogError(
                    $"[Gnomes] GnomesSceneManager: pooled prefab '{prefab.name}' " +
                    "has no Actor component.");
                Pooler.Despawn(go);
                yield break;
            }

            // Pooled objects are reused — if this instance was previously
            // identified for save data, give it a fresh identity unless the
            // game explicitly wants continuity (handled by GnomesIdentity
            // itself via RestoreGuid when driven by the save system instead).
            var identity = go.GetComponent<GnomesIdentity>();
            if (identity != null && !identity.IsSpawned)
                identity.AssignNewGuid();

            slot.CurrentActor = actor;
            slot.ActorPrefab  = prefab;

            yield return null;
        }

        /// <summary>
        /// Establishes the Player ↔ Actor ↔ PlayerBrain relationship for a slot
        /// after its actor exists in the new scene. Creates a fresh PlayerBrain
        /// instance (never shares the asset directly — same rule as everywhere
        /// else PlayerBrain is bound) and calls BindPlayer so InputLinkers wire
        /// up correctly for this specific player's device.
        /// </summary>
        private void RebindSlotBrain(GnomesPlayerSlot slot)
        {
            if (slot.CurrentActor == null || slot.BrainAsset == null) return;

            var brainInstance = Instantiate(slot.BrainAsset);
            brainInstance.IsInstance = true;
            brainInstance.Player     = slot.Player;

            slot.CurrentActor.SwapBrain(brainInstance);
        }

        // ── System reinitialization ───────────────────────────────────────────

        private IEnumerator ReinitializeSystems()
        {
            // Audio managers and other DDOL singletons react to OnSceneReady
            // via their own subscriptions. One frame delay lets scene objects
            // (environment zones etc.) finish their own Awake/Start first.
            yield return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetAllPlayersInputEnabled(bool enabled)
        {
            foreach (var slot in _playerSlots.Values)
            {
                if (slot.Player == null) continue;
                var playerInput = slot.Player.GetComponent<PlayerInput>();
                if (playerInput != null) playerInput.enabled = enabled;
            }
        }
    }
}