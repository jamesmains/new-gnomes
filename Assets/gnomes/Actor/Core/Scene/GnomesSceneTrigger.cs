using System.Linq;
using GNOMES.Actor.Core.Interaction;
using UnityEngine;

namespace GNOMES.Actor.Core.Scene {
    /// <summary>
    /// Trigger zone that initiates a scene transition when a qualifying actor
    /// enters. Built on top of GnomesWalkoverTrigger — supports dwell time,
    /// actor filtering, and progress events from that system for free.
    ///
    /// Setup:
    ///   - Add alongside a Collider set to Is Trigger
    ///   - Set DestinationScene to the scene name to load
    ///   - Configure TransitionConfig as needed
    ///   - Optionally set DwellTime on the WalkoverTrigger for hold-to-enter
    ///
    /// The dwell progress can drive a door charge-up UI via
    /// GnomesWalkoverTrigger.OnProgressChanged.
    /// </summary>
    [RequireComponent(typeof(GnomesWalkoverTrigger))]
    public class GnomesSceneTrigger : MonoBehaviour
    {
        [Header("Destination")]
        public string DestinationScene;

        [Header("Transition")]
        public GnomesTransitionConfig TransitionConfig = new();

        [Header("Cooldown")]
        public float Cooldown = 2f;

        private GnomesWalkoverTrigger _walkover;
        private float                 _lastTriggerTime = -999f;

        private void Awake()
        {
            _walkover = GetComponent<GnomesWalkoverTrigger>();
            _walkover.OnComplete += OnWalkoverComplete;
        }

        private void OnDestroy()
        {
            if (_walkover != null) _walkover.OnComplete -= OnWalkoverComplete;
        }

        private void OnWalkoverComplete(Actor actor)
        {
            if (string.IsNullOrEmpty(DestinationScene))
            {
                Debug.LogWarning(
                    $"[Gnomes] GnomesSceneTrigger on '{name}': no DestinationScene set.");
                return;
            }

            if (Time.time - _lastTriggerTime < Cooldown) return;
            _lastTriggerTime = Time.time;

            GnomesSceneManager.LoadScene(DestinationScene, TransitionConfig);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DestinationScene)) return;
            bool found = UnityEditor.EditorBuildSettings.scenes.Any(s =>
                System.IO.Path.GetFileNameWithoutExtension(s.path) == DestinationScene);

            if (!found)
                Debug.LogWarning(
                    $"[Gnomes] GnomesSceneTrigger on '{name}': " +
                    $"'{DestinationScene}' is not in Build Settings.");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 0.2f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 0.8f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.8f, $"→ {DestinationScene}");
        }
#endif
    }
}