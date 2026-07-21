using System;
using Gnomes.Demo.Modules;
using GNOMES.Input;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core.Interaction.Demo.Scripts {
    /// <summary>
    /// Raycasts from the actor's camera each frame to find GnomesInteractable
    /// objects in range. Manages hover state on InteractionModule and fires
    /// interactions when the Interact intent triggers.
    ///
    /// BehaviourOrder(50) — runs after movement and camera so the raycast
    /// uses the camera's final position for the frame.
    ///
    /// Requires: InteractionModule on the brain.
    /// Requires: CameraModule on the brain (for raycast origin/direction).
    /// </summary>
    [Serializable]
    [RequiresModule(typeof(InteractModule))]
    [RequiresModule(typeof(CameraModule))]
    [BehaviourOrder(50)]
    public class InteractorBehavior : ActorBehavior
    {
        // ── Settings ──────────────────────────────────────────────────────────

        [Header("Detection")]
        [Tooltip("Maximum distance to detect interactables.")]
        public float Range = 3f;

        [Tooltip("Layer mask for the interaction raycast. " +
                 "Set to only hit layers that contain interactables.")]
        public LayerMask InteractionMask = ~0;

        [Tooltip("If true, uses a sphere cast instead of a raycast for " +
                 "more forgiving detection. Radius controlled by SphereRadius.")]
        public bool UseSpherecast;

        [Tooltip("Radius of the sphere cast when UseSpherecast is true.")]
        public float SphereRadius = 0.15f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private InteractModule     _interactModule;
        private CameraModule          _cameraModule;

        // Intent references — resolved from InteractionModule in OnPostBind
        private IntentTrigger         _interactIntent;
        private IntentTrigger         _interactAltIntent;

        private GnomesInteractable    _currentHover;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override bool VerifyRequirements()
        {
            _interactModule = GetModule<InteractModule>();
            _cameraModule      = GetModule<CameraModule>();

            if (_interactModule == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] InteractorBehavior on '{Parent?.name}': " +
                    "no InteractionModule on brain.");
                return false;
            }

            if (_cameraModule == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] InteractorBehavior on '{Parent?.name}': " +
                    "no CameraModule on brain. " +
                    "Raycast needs a camera for origin and direction.");
                return false;
            }

            return true;
        }

        protected override void OnPostBind()
        {
            // Resolve intent triggers from InteractionModule
            // Null-safe — missing if wizard hasn't mapped them yet
            _interactIntent    = IntentTrigger.ResolveIntentTrigger(_interactModule,"Interact");
            _interactAltIntent = IntentTrigger.ResolveIntentTrigger(_interactModule,"InteractAlt");

            if (_interactIntent != null)
                _interactIntent.OnTriggered += OnPrimaryInteract;

            if (_interactAltIntent != null)
                _interactAltIntent.OnTriggered += OnSecondaryInteract;
        }

        public override void Unbind()
        {
            // Clear hover on unbind so the interactable doesn't stay highlighted
            SetHover(null);

            if (_interactIntent != null)
                _interactIntent.OnTriggered -= OnPrimaryInteract;

            if (_interactAltIntent != null)
                _interactAltIntent.OnTriggered -= OnSecondaryInteract;

            _interactIntent    = null;
            _interactAltIntent = null;
            base.Unbind();
        }

        // ── Update — raycast and hover management ─────────────────────────────

        public override void Update()
        {
            if (!IsValid) return;
            if (_cameraModule?.Camera == null) return;

            var cam = _cameraModule.Camera;
            var ray = new Ray(cam.transform.position, cam.transform.forward);

            GnomesInteractable hit = null;

            if (UseSpherecast)
            {
                if (Physics.SphereCast(ray, SphereRadius, out var hitInfo,
                        Range, InteractionMask))
                    hit = hitInfo.collider.GetComponentInParent<GnomesInteractable>();
            }
            else
            {
                if (Physics.Raycast(ray, out var hitInfo, Range, InteractionMask))
                    hit = hitInfo.collider.GetComponentInParent<GnomesInteractable>();
            }
            
            // Only hover interactables that have at least one enabled option
            if (hit != null && hit.GetEnabledOptions().Count == 0)
                hit = null;
            
            SetHover(hit);
        }

        // ── Interaction triggers ──────────────────────────────────────────────

        private void OnPrimaryInteract()
        {
            if (_currentHover == null) return;
            _currentHover.TriggerPrimary(Parent);
        }

        private void OnSecondaryInteract()
        {
            if (_currentHover == null) return;
            // Secondary fires the second highest-priority option
            _currentHover.TriggerAtIndex(1, Parent);
        }

        // ── Hover state management ────────────────────────────────────────────

        private void SetHover(GnomesInteractable target)
        {
            if (_currentHover == target) return;

            if (_currentHover != null)
                _currentHover.NotifyHoverExit(Parent);
            
            _currentHover = target;
            _interactModule.CurrentHover.Value = target;
            
            if (_currentHover != null)
                _currentHover.NotifyHoverEnter(Parent);
        }
    }
}