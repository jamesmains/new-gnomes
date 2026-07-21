using UnityEngine;

namespace GNOMES.Actor.Core.Scene {
    /// <summary>
    /// Tracks the Player ↔ Actor ↔ PlayerBrain relationship for one local
    /// player slot. GnomesSceneManager keeps one of these per joined player
    /// and uses it to drive pooled spawn/despawn and brain rebinding across
    /// scene transitions.
    ///
    /// A "slot" persists for the lifetime of a local player session even
    /// though the underlying Actor may be despawned and respawned multiple
    /// times (Respawn mode) or may persist across scenes (Persist mode).
    /// </summary>
    public class GnomesPlayerSlot
    {
        /// <summary>Stable index for this local player — 0, 1, 2... Matches GnomesSpawnPoint.PlayerIndex.</summary>
        public int PlayerIndex;

        /// <summary>The Player component owning input/device for this slot. Persists across transitions.</summary>
        public Player Player;

        /// <summary>The PlayerBrain asset (template) this slot uses. Persists across transitions.</summary>
        public PlayerBrain BrainAsset;

        /// <summary>The currently spawned Actor body for this slot. Null if despawned.</summary>
        public Actor CurrentActor;

        /// <summary>The prefab used to spawn CurrentActor — needed for pooled respawn.</summary>
        public GameObject ActorPrefab;
    }
}