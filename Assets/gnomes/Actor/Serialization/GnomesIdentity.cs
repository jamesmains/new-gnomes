using UnityEngine;

namespace GNOMES.Actor.Serialization {
    /// <summary>
    /// Gives an actor a stable identity that survives scene loads and
    /// domain reloads. Required for the save system to match save data
    /// back to the right actor on load.
    ///
    /// Scene actors: GUID is assigned in the editor and serialized.
    /// Spawned actors: GUID is generated at spawn time via AssignNewGuid().
    ///
    /// Add this component alongside Actor on any saveable actor prefab.
    /// </summary>
    public class GnomesIdentity : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private string _guid;

        /// <summary>The actor's stable unique identifier.</summary>
        public string Guid => _guid;

        /// <summary>True if this actor was dynamically spawned (not a scene object).</summary>
        public bool IsSpawned { get; private set; }

        /// <summary>
        /// The prefab path used to spawn this actor.
        /// Null for scene actors. Set by GnomesSaveSystem on spawn.
        /// </summary>
        public string PrefabPath { get; set; }

        private void Awake()
        {
            // Scene actors get their GUID assigned in the editor via
            // AssignGuidIfEmpty (called from a custom editor or Reset).
            // Spawned actors should call AssignNewGuid() after instantiation.
            if (string.IsNullOrEmpty(_guid))
                AssignNewGuid();
        }

        /// <summary>
        /// Generates a new GUID. Called automatically in Awake if empty,
        /// and explicitly by GnomesSaveSystem when spawning a saved actor.
        /// </summary>
        public void AssignNewGuid()
        {
            _guid     = System.Guid.NewGuid().ToString();
            IsSpawned = true;
        }

        /// <summary>
        /// Restores a specific GUID — used by the load system to re-identity
        /// a newly spawned actor with its saved GUID.
        /// </summary>
        internal void RestoreGuid(string guid)
        {
            _guid     = guid;
            IsSpawned = true;
        }

#if UNITY_EDITOR
        // Assign a GUID in the editor when the component is first added
        // or when Reset is called, so scene actors always have one.
        private void Reset() => AssignGuidIfEmpty();

        [UnityEditor.MenuItem("CONTEXT/GnomesIdentity/Regenerate GUID")]
        private static void RegenerateGuid(UnityEditor.MenuCommand cmd)
        {
            var identity = cmd.context as GnomesIdentity;
            if (identity == null) return;

            if (!UnityEditor.EditorUtility.DisplayDialog(
                    "Regenerate GUID",
                    "This will break any existing save data for this actor. Continue?",
                    "Regenerate", "Cancel"))
                return;

            identity._guid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(identity);
        }
#endif

        internal void AssignGuidIfEmpty()
        {
            if (string.IsNullOrEmpty(_guid))
                _guid = System.Guid.NewGuid().ToString();
        }
    }
}