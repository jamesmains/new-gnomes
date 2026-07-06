using GNOMES.Actor.Core;
using Gnomes.Demo.Modules;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Behaviors {
    
    /// <summary>
    /// Sample movement behavior.
    /// </summary>
    [RequiresModule(typeof(MovementModule))] 
    [RequiresModule(typeof(CameraModule))]
    public class Movement3dSimpleBehavior : ActorBehavior {
        [Header("Settings")] public float BaseSpeed = 20f;
        public float MaxVelocity = 12f;
        public float Gravity = 12f;
        public float Acceleration = 18f;
        public float JumpForce = 6f;

        // Dependencies — resolved in VerifyRequirements
        private Rigidbody _rb;
        private Collider _col;
        private MovementModule _movementModule;

        private Vector3 _currentVelocity;
        private bool _jumpQueued;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override bool VerifyRequirements() {
            _rb = Parent.GetComponent<Rigidbody>();
            _col = Parent.GetComponent<Collider>();
            _movementModule = GetModule<MovementModule>();
            return _rb && _col && _movementModule != null;
        }

        protected override void OnPostBind() {

             //Subscribe to jump trigger — queues jump for next FixedUpdate
            if (_movementModule != null)
                _movementModule.Jump.OnTriggered += OnJumpTriggered;
        }

        public override void Unbind() {
            base.Unbind();
        }

        private void OnJumpTriggered() => _jumpQueued = true;

        // ── FixedUpdate ───────────────────────────────────────────────────────

        public override void FixedUpdate() {
            if (!IsValid) return;

            // Read move intent — direct field access, zero overhead
            // If _intents is null (brain doesn't support locomotion intents)
            // we get zero input and the actor stands still — safe fallback.
            Vector2 moveInput = _movementModule.Move.Value;
            HandleMovement(moveInput);
            HandleJump();
        }

        private void HandleMovement(Vector2 moveInput) {
            Vector3 moveDir =
                Parent.transform.forward * (moveInput.y * BaseSpeed * Time.fixedDeltaTime) +
                Parent.transform.right * (moveInput.x * BaseSpeed * Time.fixedDeltaTime);

            _currentVelocity = Vector3.Lerp(
                _currentVelocity, moveDir, Acceleration * Time.fixedDeltaTime);

            _rb.AddForce(_currentVelocity, ForceMode.VelocityChange);
            _movementModule.Velocity.Value = _currentVelocity;
        }

        private void HandleJump() {
            if (!_jumpQueued) return;
            _jumpQueued = false;

            if (!_movementModule.IsGrounded.Value) return;

            _rb.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
        }
    }
}