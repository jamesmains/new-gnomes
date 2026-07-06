using System;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core.Interaction {
    /// <summary>
    /// ActorBehavior that tracks active inspection state and handles
    /// automatic exit conditions (radius and timeout).
    ///
    /// Add to the actor's InitialBehaviors. Stays dormant when not inspecting.
    ///
    /// BehaviourOrder(40) — runs before InteractorBehavior(50) so exit
    /// conditions are evaluated before new interactions can fire.
    ///
    /// Does not require any modules — works independently of brain state.
    /// </summary>
    [Serializable]
    [BehaviourOrder(40)]
    public class GnomesInspectBehavior : ActorBehavior
    {
        // ── Runtime state ─────────────────────────────────────────────────────

        private GnomesInspectPoint _activePoint;
        private ActorBrain         _inspectBrainInstance;
        private float              _inspectStartTime;
        private bool               _isInspecting;
        // ── Public state ──────────────────────────────────────────────────────

        public bool               IsInspecting  => _isInspecting;
        public GnomesInspectPoint ActivePoint   => _activePoint;

        /// <summary>
        /// How long the actor has been in the current inspection session.
        /// </summary>
        public float InspectDuration =>
            _isInspecting ? Time.time - _inspectStartTime : 0f;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when this actor begins any inspection.
        /// Useful for disabling movement UI, crosshair etc.
        /// </summary>
        public event Action<GnomesInspectPoint> OnInspectionBegan;

        /// <summary>Fired when this actor exits any inspection.</summary>
        public event Action OnInspectionEnded;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override bool VerifyRequirements() => true;
        // No module requirements — inspect behavior works on any actor

        public override void Unbind()
        {
            // If the brain is swapped externally while inspecting, clean up
            if (_isInspecting)
                OnInspectionEnded?.Invoke();

            Debug.Log($"[Gnomes] GnomesInspectBehavior: Ending inspection due to unbind of {_inspectBrainInstance} on point {_activePoint}.");
            _isInspecting        = false;
            _activePoint         = null;
            _inspectBrainInstance = null;
            
            base.Unbind();
        }

        // ── Called by GnomesInspectPoint ──────────────────────────────────────

        internal void OnInspectionStarted(
            GnomesInspectPoint point,
            ActorBrain brainInstance)
        {
            _activePoint          = point;
            _inspectBrainInstance = brainInstance;
            _inspectStartTime     = Time.time;
            _isInspecting         = true;

            OnInspectionBegan?.Invoke(point);

            Debug.Log($"[Gnomes] GnomesInspectBehavior: Starting inspection of {brainInstance} on point {point}. Inspecting? {_isInspecting}");
        }

        internal void HandleOnInspectionEnded()
        {
            Debug.Log($"[Gnomes] GnomesInspectBehavior: Ending inspection of {_inspectBrainInstance} on point {_activePoint}.");

            _isInspecting         = false;
            _activePoint          = null;
            _inspectBrainInstance = null;

            OnInspectionEnded?.Invoke();
        }

        // ── Update — exit condition checks ────────────────────────────────────

        public override void Update()
        {
            if (!IsValid || !_isInspecting || _activePoint == null) {
                return;
            }
            CheckExitRadius();
            CheckExitTimeout();
        }

        private void CheckExitRadius()
        {
            if (_activePoint.ExitRadius <= 0f) return;

            float dist = Vector3.Distance(
                Parent.transform.position,
                _activePoint.transform.position);

            if (dist > _activePoint.ExitRadius)
            {
                Debug.Log(
                    $"[Gnomes] '{Parent.name}' exited inspection of " +
                    $"'{_activePoint.name}' — exceeded exit radius.");
                _activePoint.ExitInspection(Parent);
            }
        }

        private void CheckExitTimeout()
        {
            if (_activePoint.ExitTimeout <= 0f) return;

            if (InspectDuration >= _activePoint.ExitTimeout)
            {
                Debug.Log(
                    $"[Gnomes] '{Parent.name}' exited inspection of " +
                    $"'{_activePoint.name}' — timeout reached.");
                _activePoint.ExitInspection(Parent);
            }
        }

#if UNITY_EDITOR
        // Draw exit radius gizmo on the inspect point for easy configuration
        private void OnDrawGizmosSelected()
        {
            if (_activePoint == null) return;
            if (_activePoint.ExitRadius <= 0f) return;

            UnityEditor.Handles.color = new Color(0.22f, 0.78f, 0.93f, 0.3f);
            UnityEditor.Handles.DrawWireDisc(
                _activePoint.transform.position,
                Vector3.up,
                _activePoint.ExitRadius);
        }
#endif
    }
}