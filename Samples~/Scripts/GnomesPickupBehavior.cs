using System;
using Gnomes.Demo.Modules;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core.Interaction.Demo.Scripts {
    /// <summary>
    /// Manages picking up and dropping GnomesInteractables.
    ///
    /// Two hold modes (set HoldMode in inspector):
    ///   Attached    — object snaps to a HoldTransform on the actor/rig.
    ///                 Good for third-person or hand-bone attachment.
    ///   CameraRelative — object floats at HoldDistance in front of the camera.
    ///                 Good for first-person carry.
    ///
    /// Pickup automatically:
    ///   - Disables physics on the held object
    ///   - Switches the interactable to its "Held" option set
    ///   - Wires the Drop callback on the "Drop" option if present
    ///
    /// Drop automatically:
    ///   - Re-enables physics
    ///   - Switches back to "Default" option set
    ///   - Restores original parent
    /// </summary>
    [Serializable]
    [RequiresModule(typeof(InteractModule))]
    [RequiresModule(typeof(CameraModule))]
    [BehaviourOrder(60)]   // after InteractorBehavior(50)
    public class GnomesPickupBehavior : ActorBehavior
    {
        // ── Settings ──────────────────────────────────────────────────────────
 
        public enum PickupHoldMode { Attached, CameraRelative }
 
        [Header("Hold Mode")]
        [Tooltip("Attached: snaps to HoldTransform. " +
                 "CameraRelative: floats in front of the camera.")]
        public PickupHoldMode HoldMode = PickupHoldMode.CameraRelative;
 
        [Header("Attached Mode")]
        [Tooltip("Transform the held object is parented to in Attached mode. " +
                 "Typically a hand bone or a child of the actor.")]
        public Transform HoldTransform;
 
        [Header("Camera Relative Mode")]
        [Tooltip("Distance in front of the camera to hold the object.")]
        public float HoldDistance = 1.5f;
 
        [Tooltip("How quickly the held object moves to its target position.")]
        public float HoldSmoothing = 20f;
 
        [Header("Pickup")]
        [Tooltip("Name of the option on the interactable that triggers pickup.")]
        public string PickupOptionLabel = "Pick Up";
 
        [Tooltip("Name of the option set to activate on the held object when picked up.")]
        public string HeldSetName = "Held";
 
        [Tooltip("Name of the option on the held object that triggers drop.")]
        public string DropOptionLabel = "Drop";
 
        // ── Runtime ───────────────────────────────────────────────────────────
 
        private InteractModule  _interactionModule;
        private CameraModule       _cameraModule;
 
        private GnomesInteractable _heldInteractable;
        private Rigidbody          _heldRigidbody;
        private Transform          _originalParent;
        private bool               _wasKinematic;
        private bool               _usedGravity;
 
        // ── Events ────────────────────────────────────────────────────────────
 
        /// <summary>Fired when an object is picked up.</summary>
        public event Action<GnomesInteractable> OnPickedUp;
 
        /// <summary>Fired when the held object is dropped.</summary>
        public event Action<GnomesInteractable> OnDropped;
 
        public bool IsHolding => _heldInteractable != null;
        public GnomesInteractable HeldObject => _heldInteractable;
 
        // ── Lifecycle ─────────────────────────────────────────────────────────
 
        protected override bool VerifyRequirements()
        {
            _interactionModule = GetModule<InteractModule>();
            _cameraModule      = GetModule<CameraModule>();
 
            if (_interactionModule == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] GnomesPickupBehavior on '{Parent?.name}': " +
                    "no InteractionModule.");
                return false;
            }
 
            return true;
        }
 
        protected override void OnPostBind()
        {
            // Subscribe to hover changes so we can wire/unwire the pickup
            // callback as the player looks at different objects
            _interactionModule.CurrentHover.OnChanged += OnHoverChanged;
        }
 
        public override void Unbind()
        {
            // Drop anything held when the brain is swapped
            if (_heldInteractable != null) Drop();
 
            if (_interactionModule != null)
                _interactionModule.CurrentHover.OnChanged -= OnHoverChanged;
 
            base.Unbind();
        }
 
        // ── Update — held object positioning ─────────────────────────────────
 
        public override void FixedUpdate()
        {
            if (!IsValid || _heldInteractable == null) return;
 
            if (HoldMode == PickupHoldMode.CameraRelative)
                UpdateCameraRelativeHold();
        }
 
        private void UpdateCameraRelativeHold()
        {
            if (_cameraModule?.Camera == null) return;
 
            var cam         = _cameraModule.Camera.transform;
            var targetPos   = cam.position + cam.forward * HoldDistance;
            var heldTf      = _heldInteractable.transform;
 
            // Smooth follow — feels natural without being floaty
            heldTf.position = Vector3.Lerp(
                heldTf.position, targetPos,
                Time.fixedDeltaTime * HoldSmoothing);
        }
 
        // ── Hover change — wire pickup option ─────────────────────────────────
 
        private void OnHoverChanged(GnomesInteractable interactable)
        {
            if (interactable == null) return;
 
            // Find the pickup option and wire our Pickup callback to it
            foreach (var set in interactable.OptionSets)
            {
                foreach (var opt in set.Options)
                {
                    if (opt.Label != PickupOptionLabel) continue;
                    // Avoid double-subscribing
                    opt.OnInteractCallback -= Pickup;
                    opt.OnInteractCallback += Pickup;
                }
            }
        }
 
        // ── Pickup ────────────────────────────────────────────────────────────
 
        private void Pickup(Actor actor)
        {
            if (_heldInteractable != null) Drop();
 
            var hover = _interactionModule.CurrentHover.Value;
            if (hover == null) return;
 
            _heldInteractable = hover;
            var tf = _heldInteractable.transform;
 
            // Store original parent for restore on drop
            _originalParent = tf.parent;
 
            // Disable physics
            _heldRigidbody = _heldInteractable.GetComponent<Rigidbody>();
            if (_heldRigidbody != null)
            {
                _wasKinematic = _heldRigidbody.isKinematic;
                _usedGravity  = _heldRigidbody.useGravity;
                _heldRigidbody.isKinematic = true;
                _heldRigidbody.useGravity  = false;
                _heldRigidbody.linearVelocity        = Vector3.zero;
                _heldRigidbody.angularVelocity = Vector3.zero;
            }
 
            if (HoldMode == PickupHoldMode.Attached && HoldTransform != null)
            {
                tf.SetParent(HoldTransform, worldPositionStays: true);
            }
            
            // Switch the interactable to its "Held" option set
            _heldInteractable.ActivateSet(HeldSetName);
 
            // Invoke change event to update anything listening to prompts
            _interactionModule.CurrentHover.OnChanged.Invoke(_heldInteractable);

            // Wire the Drop callback onto the drop option
            WireDropOption();
 
            OnPickedUp?.Invoke(_heldInteractable);
        }
 
        // ── Drop ──────────────────────────────────────────────────────────────
 
        public void Drop()
        {
            if (_heldInteractable == null) return;
 
            var dropped = _heldInteractable;
            var tf      = dropped.transform;
 
            // Restore parent
            tf.SetParent(_originalParent, worldPositionStays: true);
 
            // Restore physics
            if (_heldRigidbody != null)
            {
                _heldRigidbody.isKinematic = _wasKinematic;
                _heldRigidbody.useGravity  = _usedGravity;
                _heldRigidbody = null;
            }
 
            // Restore default option set
            dropped.ActivateSet("Default");
            
            // Invoke change event to update anything listening to prompts
            _interactionModule.CurrentHover.OnChanged.Invoke(_heldInteractable);
            
            _heldInteractable = null;
            _originalParent   = null;

            OnDropped?.Invoke(dropped);
        }
 
        private void WireDropOption()
        {
            if (_heldInteractable == null) return;
 
            foreach (var set in _heldInteractable.OptionSets)
            {
                foreach (var opt in set.Options)
                {
                    if (opt.Label != DropOptionLabel) continue;
                    opt.OnInteractCallback -= _ => Drop();
                    opt.OnInteractCallback += _ => Drop();
                }
            }
        }
    }
}