using System;
using UnityEngine;

namespace GNOMES.Actor.Core.Scene
{
    
    /// <summary>
    /// Configuration for a scene transition. Pass to GnomesSceneManager.LoadScene.
    /// All fields have sensible defaults so most calls only need the scene name.
    ///
    /// Usage:
    ///   // Simple — uses manager defaults
    ///   GnomesSceneManager.LoadScene("ForestLevel");
    ///
    ///   // Custom
    ///   GnomesSceneManager.LoadScene("ForestLevel", new GnomesTransitionConfig
    ///   {
    ///       PersistenceMode = PlayerPersistenceMode.Persist,
    ///       SpawnPointId    = "CaveEntrance",
    ///       SaveBeforeLoad  = true
    ///   });
    /// </summary>
    [Serializable]
    public class GnomesTransitionConfig
    {
        public PlayerPersistenceMode PersistenceMode = PlayerPersistenceMode.Respawn;

        /// <summary>
        /// ID of the spawn point to place players at. Empty = highest-priority
        /// point found. For multiple players, GnomesSpawnPoint.PlayerIndex is
        /// matched against each player's slot index.
        /// </summary>
        public string SpawnPointId = "";

        public bool SaveBeforeLoad = false;
        public bool LoadAfterReady = false;

        /// <summary>
        /// Prefab to pool-spawn for the player in Respawn mode.
        /// Must have Actor + GnomesIdentity. If null, uses
        /// GnomesSceneManager.DefaultPlayerPrefab.
        /// </summary>
        public GameObject PlayerPrefabOverride = null;

        /// <summary>
        /// If true, the destination scene loads additively alongside the
        /// current one rather than replacing it. Use with UnloadScene to
        /// manage streaming/layered scenes explicitly.
        /// </summary>
        public bool Additive = false;

        public static GnomesTransitionConfig Default => new();
    }
}