using System.Linq;
using UnityEngine;

namespace GNOMES.Actor.Core.Scene {
    /// <summary>
    /// Marks a spawn location in a scene. The scene manager finds the best
    /// matching spawn point when placing the player after a transition.
    ///
    /// Multiple spawn points can share the same ID — the one with the highest
    /// Priority wins. Useful for having a "default" spawn and named overrides.
    /// </summary>
    public class GnomesSpawnPoint : MonoBehaviour
    {
        [Tooltip("Identifier used to find this spawn point.")]
        public string SpawnId = "Default";

        [Tooltip("Higher priority wins when multiple spawn points share the same ID.")]
        public int Priority = 0;

        [Tooltip("Which local player slot this spawn point is reserved for. " +
                 "-1 = any player.")]
        public int PlayerIndex = -1;

        /// <summary>
        /// Finds the best spawn point for a given ID and player slot index.
        /// Player-specific points are preferred over generic ones.
        /// </summary>
        public static GnomesSpawnPoint Find(string spawnId, int playerIndex)
        {
            var all = FindObjectsByType<GnomesSpawnPoint>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (all.Length == 0) return null;

            // Prefer points explicitly reserved for this player index
            var exact = all
                .Where(s => s.PlayerIndex == playerIndex)
                .Where(s => string.IsNullOrEmpty(spawnId) || s.SpawnId == spawnId)
                .OrderByDescending(s => s.Priority)
                .FirstOrDefault();
            if (exact != null) return exact;

            // Fall back to ID match with any player index
            var idMatch = all
                .Where(s => !string.IsNullOrEmpty(spawnId) && s.SpawnId == spawnId)
                .Where(s => s.PlayerIndex == -1)
                .OrderByDescending(s => s.Priority)
                .FirstOrDefault();
            if (idMatch != null) return idMatch;

            // Last resort — any point, highest priority
            return all.OrderByDescending(s => s.Priority).First();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.22f, 0.78f, 0.93f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.8f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                $"Spawn: {SpawnId}" +
                (PlayerIndex >= 0 ? $" (P{PlayerIndex})" : ""));
        }
#endif
    }
}