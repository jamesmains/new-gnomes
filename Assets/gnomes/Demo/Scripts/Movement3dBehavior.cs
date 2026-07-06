 
// ── Movement3dBehavior ────────────────────────────────────────────────────────

using System;
using GNOMES.Actor.Core;
using Gnomes.Demo.Modules;
using GNOMES.Input;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Demo.Scripts
{
    /// <summary>
    /// Handles 3D rigidbody movement including ground detection, slope handling,
    /// and jumping. Reads Move and Jump intents from LocomotionModule.
    ///
    /// Requires: Rigidbody and Collider on the actor's GameObject.
    /// Requires: LocomotionModule on the brain.
    ///
    /// NOTE: Disable Rigidbody.useGravity — this behaviour owns gravity
    /// entirely to avoid double-application with Unity's built-in gravity.
    ///
    /// BehaviourOrder(-100) ensures movement runs before camera every frame.
    /// </summary>
    [Serializable]
    [RequiresModule(typeof(MovementModule))]
    [BehaviourOrder(-100)]
    public class Movement3dBehavior : ActorBehavior
    {
        // ── Ground detection state ────────────────────────────────────────────
 
        [Serializable]
        private struct GroundInfo
        {
            public bool       OnGround;
            public bool       OnSlope;
            public float      SlopeAngle;
            public Vector3    SlopeNormal;
            public Vector3    SlopeDownVector;
            public RaycastHit GroundHit;
        }
 
        // ── Settings ──────────────────────────────────────────────────────────
 
        [Header("Movement")]
        public float BaseSpeed    = 20f;
        public float MaxVelocity  = 12f;
        public float Acceleration = 18f;
        public float Deceleration = 14f;
        public float AirControl   = 0.35f;
 
        [Header("Gravity")]
        public float Gravity = 12f;
 
        [Header("Slope")]
        [Tooltip("Angle range for slope speed reduction. X = start losing speed, Y = max climbable angle.")]
        public Vector2 SlopeRange;
 
        [Header("Jump")]
        public float JumpForce = 6f;
 
        [Header("Wall Glide")]
        [Tooltip("Surfaces steeper than this are treated as walls rather than " +
                 "slopes/floor, independent of SlopeRange. Keeping this separate " +
                 "from SlopeRange.y avoids coupling wall detection to slope-climb " +
                 "tuning — changing one no longer silently breaks the other.")]
        public float WallAngleThreshold = 75f;
 
        [Tooltip("Radius of the sphere cast used to detect walls ahead of the actor.")]
        public float WallRayRadius = 0.4f;
 
        [Tooltip("Distance ahead to check for walls.")]
        public float WallRayLength = 0.3f;
 
        [Tooltip("Below this approach angle (degrees off dead-on) the actor " +
                 "glides at full speed. Above it, speed ramps down toward the " +
                 "dead-on stop. Keeps shallow grazing contact from feeling sticky " +
                 "while a true head-on hit still stops the actor.")]
        [Range(1f, 89f)]
        public float WallGlideFullSpeedAngle = 35f;
 
        [Header("Ground Detection")]
        [Tooltip("How many consecutive FixedUpdate frames a ground-state change " +
                 "must persist before it takes effect. Prevents grounded/airborne " +
                 "flicker at edges and corners where the sphere cast result can " +
                 "vary frame to frame against angled geometry.")]
        [Range(0, 5)]
        public int GroundedDebounceFrames = 2;

        public float GroundRayLength = 0.6f;
 
        // ── Runtime dependencies ──────────────────────────────────────────────
        // Resolved in VerifyRequirements — not serialized
 
        private Rigidbody        _rigidbody;
        private Collider         _collider;
        private MovementModule _loco;
 
        // ── Intent references — resolved once in OnPostBind ───────────────────
        // Written by brain/linkers, read here every FixedUpdate
 
        private IntentValue<Vector2> _moveIntent;
        private IntentTrigger        _jumpIntent;
 
        // ── Internal state ────────────────────────────────────────────────────
 
        private Vector3    _targetVelocity;
        private Vector3    _currentVelocity;
        private GroundInfo _groundInfo;
        private bool       _jumpQueued;
 
        // Debounce state for grounded transitions — see GroundedDebounceFrames
        private bool _pendingGroundState;
        private int  _groundStateStableFrames;
 
        // ── Lifecycle ─────────────────────────────────────────────────────────
 
        protected override bool VerifyRequirements()
        {
            _rigidbody = Parent.GetComponent<Rigidbody>();
            _collider  = Parent.GetComponent<Collider>();
            _loco      = GetModule<MovementModule>();
 
            if (_rigidbody == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] Movement3dBehavior on '{Parent?.name}': " +
                    "no Rigidbody found.");
                return false;
            }
 
            if (_collider == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] Movement3dBehavior on '{Parent?.name}': " +
                    "no Collider found.");
                return false;
            }
 
            if (_loco == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] Movement3dBehavior on '{Parent?.name}': " +
                    "no LocomotionModule on brain.");
                return false;
            }
 
            return true;
        }
 
        protected override void OnPostBind()
        {
            _groundInfo              = default;
            _jumpQueued              = false;
            _currentVelocity         = Vector3.zero;
            _pendingGroundState      = false;
            _groundStateStableFrames = 0;
 
            // Resolve intent fields from the module — typed, no string keys.
            // Returns null if the wizard hasn't added these fields yet;
            // movement and jump degrade gracefully to zero input.
            _moveIntent = IntentTrigger.ResolveIntent<Vector2>(_loco, "Move");
            _jumpIntent = IntentTrigger.ResolveIntentTrigger(_loco, "Jump");
 
            // Subscribe to jump trigger — queued for next FixedUpdate
            if (_jumpIntent != null)
                _jumpIntent.OnTriggered += OnJumpTriggered;
        }
 
        public override void Unbind()
        {
            if (_jumpIntent != null)
                _jumpIntent.OnTriggered -= OnJumpTriggered;
 
            _moveIntent = null;
            _jumpIntent = null;
            base.Unbind();
        }
 
        private void OnJumpTriggered() => _jumpQueued = true;
 
        // ── FixedUpdate ───────────────────────────────────────────────────────
 
        public override void FixedUpdate()
        {
            if (!IsValid) return;
 
            HandleRotation();
            HandleMovement();
            HandleJump();
        }
 
        // ── Private movement logic ────────────────────────────────────────────
 
        private void HandleRotation()
        {
            Vector3 lookDir = _loco.FacingDirection.Value;
            lookDir.y = 0;
 
            if (lookDir.sqrMagnitude > 0.01f)
                _rigidbody.MoveRotation(Quaternion.LookRotation(lookDir));
        }
 
        private void HandleMovement()
        {
            // Null-safe: if wizard hasn't mapped Move yet, input is zero
            Vector2 moveInput = (_moveIntent?.Value ?? Vector2.zero).normalized;
 
            float groundDistance = _collider.bounds.extents.y + GroundRayLength;
            CheckGround(groundDistance);
 
            // Apply an air control multiplier when not grounded
            float speedMultiplier = _groundInfo.OnGround ? 1f : AirControl;
 
            // ── 1. Base desired direction (ground/slope relative) ──────────────
            Vector3 desiredDir;
 
            if (_groundInfo.OnSlope)
            {
                // Project movement onto slope plane so the actor doesn't
                // fight the slope normal
                var slopeForward = Vector3.ProjectOnPlane(
                    Parent.transform.forward, _groundInfo.SlopeNormal).normalized;
                var slopeRight = Vector3.ProjectOnPlane(
                    Parent.transform.right, _groundInfo.SlopeNormal).normalized;
 
                desiredDir = slopeForward * moveInput.y + slopeRight * moveInput.x;
 
                // Gradually lose speed on steep slopes
                if (_groundInfo.SlopeAngle >= SlopeRange.x)
                {
                    float slopeFactor = Mathf.InverseLerp(
                        SlopeRange.x, SlopeRange.y, _groundInfo.SlopeAngle);
                    desiredDir -= _groundInfo.SlopeDownVector * slopeFactor;
                }
            }
            else
            {
                desiredDir =
                    Parent.transform.forward * moveInput.y +
                    Parent.transform.right   * moveInput.x;
            }
 
            // ── 2. Wall glide — checked every frame, not gated on input ────────
            // Running this unconditionally (using current movement/facing
            // direction even with zero input) means a wall is detected
            // consistently rather than only while actively pushing into it,
            // which previously let grounded state flicker the instant input
            // momentarily dropped to zero next to a wall.
            desiredDir = ApplyWallGlide(desiredDir, moveInput);
 
            // ── 3. Finalize target velocity ─────────────────────────────────────
            _targetVelocity = desiredDir * (BaseSpeed * speedMultiplier);
 
            // Apply gravity when airborne — behaviour owns this, so
            // Rigidbody.useGravity should be disabled on the actor.
            if (!_groundInfo.OnGround)
                _targetVelocity.y = _rigidbody.linearVelocity.y - Gravity * Time.fixedDeltaTime;
            else if (_targetVelocity.y < 0)
                _targetVelocity.y = 0f;   // don't accumulate negative Y on ground
 
            _loco.IsGrounded.Value = _groundInfo.OnGround;
 
            float lerpSpeed = _groundInfo.OnGround ? Acceleration : Deceleration;
            _currentVelocity = Vector3.Lerp(
                _currentVelocity, _targetVelocity,
                lerpSpeed * Time.fixedDeltaTime);
 
            // Apply via velocity delta rather than raw force so mass doesn't
            // skew the feel, and so we can cleanly override Y handling below
            // without fighting whatever X/Z deltas were just computed.
            Vector3 velocityChange = _currentVelocity - _rigidbody.linearVelocity;
 
            if (!_groundInfo.OnGround)
            {
                // Let gravity integrate cleanly in air — don't let the
                // horizontal lerp smoothing bleed into vertical accel/decel.
                velocityChange.y = _targetVelocity.y - _rigidbody.linearVelocity.y;
            }
            else if (_targetVelocity.y <= 0.01f && _rigidbody.linearVelocity.y < 0f)
            {
                // Snap any residual downward velocity to zero only while
                // genuinely grounded — guarded against by the debounced
                // ground state, so this no longer stutters at edges.
                velocityChange.y = -_rigidbody.linearVelocity.y;
            }
 
            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
 
            _currentVelocity      = _rigidbody.linearVelocity;
            _loco.Velocity.Value  = _currentVelocity;
        }
 
        /// <summary>
        /// Detects a wall ahead of the desired movement direction and adjusts
        /// the direction so the actor glides along the surface rather than
        /// stopping — unless the approach is close to dead-on, in which case
        /// speed ramps down toward a stop. Runs every frame regardless of
        /// input magnitude so wall contact is resolved consistently.
        /// </summary>
        private Vector3 ApplyWallGlide(Vector3 desiredDir, Vector2 moveInput)
        {
            // Nothing to glide against if there's no meaningful direction —
            // but note this still runs the cast even at low input so a wall
            // pressed against with near-zero stick deflection is still caught.
            if (desiredDir.sqrMagnitude < 0.0001f)
                return desiredDir;
 
            Vector3 castDir = desiredDir.normalized;
 
            // Cast from mid-body height so the sphere doesn't clip the floor
            // or floor lips while still catching low wall geometry.
            Vector3 rayOrigin = Parent.transform.position +
                Vector3.up * _collider.bounds.extents.y;
 
            if (!Physics.SphereCast(
                rayOrigin, WallRayRadius, castDir,
                out RaycastHit wallHit, WallRayLength))
                return desiredDir;
 
            float surfaceAngle = Vector3.Angle(Vector3.up, wallHit.normal);
 
            // Not steep enough to count as a wall — treat as walkable terrain
            // and let the slope logic above (already run) handle it instead.
            // Using a dedicated threshold here rather than SlopeRange.y keeps
            // wall detection independent from slope-climb tuning.
            if (surfaceAngle <= WallAngleThreshold)
                return desiredDir;
 
            // How directly the actor is moving into the wall.
            // 0 = perfectly parallel (grazing), 1 = perfectly dead-on.
            float dot = Vector3.Dot(castDir, -wallHit.normal);
            float approachAngleDeg = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
 
            // Project the movement onto the wall plane to get the natural
            // glide direction. We do NOT rescale this back up to the original
            // magnitude — ProjectOnPlane already gives the correct reduced
            // magnitude for the glide, so re-multiplying by the original
            // input magnitude was double-penalizing speed.
            Vector3 slideDir = Vector3.ProjectOnPlane(desiredDir, wallHit.normal);
 
            // Head-on escape: if the projection collapses to ~zero (truly
            // dead-on with no lateral component to slide along), fall back
            // to sliding along the actor's current facing projected onto the
            // wall so there's still *some* escape direction rather than a
            // hard zero vector, which previously caused the "stuck" feel.
            if (slideDir.sqrMagnitude < 0.0025f)
            {
                Vector3 facing = _loco.FacingDirection.Value;
                facing.y = 0f;
                slideDir = Vector3.ProjectOnPlane(facing, wallHit.normal);
            }
 
            // Speed retention curve — NOT linear. Shallow approach angles
            // (well under WallGlideFullSpeedAngle) keep full speed so
            // grazing a wall while strafing past it doesn't feel sticky at
            // all. Only as the approach closes in on dead-on does speed
            // ramp down, reaching ~0 right at 90° (fully head-on).
            float t = Mathf.InverseLerp(WallGlideFullSpeedAngle, 90f, approachAngleDeg);
            t = Mathf.Clamp01(t);
            // Smoothstep rather than linear — eases in/out so the transition
            // from "full glide" to "stopped" doesn't have a sharp kink.
            float speedRetained = 1f - (t * t * (3f - 2f * t));
 
            // slideDir's own magnitude already reflects the natural reduction
            // from projecting onto the wall plane — only the input's analog
            // deflection (moveInput.magnitude) and the approach-based
            // speedRetained scale it further. We do not multiply by
            // desiredDir.magnitude a second time.
            return slideDir.normalized * (moveInput.magnitude * speedRetained);
        }
 
        private void HandleJump()
        {
            if (!_jumpQueued) return;
            _jumpQueued = false;
 
            if (!_groundInfo.OnGround) return;
 
            // Impulse directly upward — bypasses the velocity lerp so it
            // feels snappy rather than being smoothed away
            _rigidbody.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
 
            // Clear downward velocity so the impulse isn't fighting gravity
            // that accumulated this frame
            _currentVelocity.y = 0f;
 
            // Immediately mark airborne (debounced) so a jump taken right at
            // a wall/edge doesn't get re-grounded next frame by a stale cast
            // result before the actor has actually left the surface.
            _pendingGroundState      = false;
            _groundStateStableFrames = GroundedDebounceFrames;
            _groundInfo.OnGround     = false;
        }
 
        private void CheckGround(float distance)
        {
            bool rawGrounded;
            RaycastHit hit = default;
 
            if (Physics.SphereCast(
                Parent.transform.position, 0.1f,
                -Parent.transform.up,
                out hit,
                distance))
            {
                float angle = Vector3.Angle(Parent.transform.up, hit.normal);
 
                // If the surface is steeper than what we can even stand on,
                // reject it as ground entirely — it's a wall, not a floor,
                // even if the sphere cast happened to clip it from above.
                // This uses the same WallAngleThreshold as ApplyWallGlide so
                // the two systems agree on what counts as a wall instead of
                // each using a different cutoff (SlopeRange.y vs a hardcoded
                // value), which was the previous source of edge inconsistency.
                rawGrounded = angle <= WallAngleThreshold;
 
                if (rawGrounded)
                {
                    _groundInfo.GroundHit = hit;
 
                    // Use a 2° threshold to avoid flat-ground floating point
                    // noise triggering slope logic on perfectly level surfaces
                    if (angle > 2f)
                    {
                        _groundInfo.OnSlope     = true;
                        _groundInfo.SlopeAngle  = angle;
                        _groundInfo.SlopeNormal = hit.normal;
 
                        Vector3 left = Vector3.Cross(
                            _groundInfo.SlopeNormal, Parent.transform.up);
                        _groundInfo.SlopeDownVector =
                            Vector3.Cross(left, _groundInfo.SlopeNormal).normalized;
                    }
                    else
                    {
                        _groundInfo.OnSlope    = false;
                        _groundInfo.SlopeAngle = 0f;
                    }
                }
            }
            else
            {
                rawGrounded = false;
            }
 
            // ── Debounce the grounded state ─────────────────────────────────────
            // A single frame's cast result near an edge/corner is unreliable —
            // require the new state to hold for GroundedDebounceFrames
            // consecutive frames before it actually changes _groundInfo.OnGround.
            // This is what fixes the grounded/airborne flicker that previously
            // prevented jumping near walls and caused vertical-velocity stutter.
            if (GroundedDebounceFrames <= 0)
            {
                _groundInfo.OnGround = rawGrounded;
                if (!rawGrounded) { _groundInfo.OnSlope = false; _groundInfo.SlopeAngle = 0f; }
                return;
            }
 
            if (rawGrounded == _groundInfo.OnGround)
            {
                // State agrees with current — reset any pending change attempt
                _groundStateStableFrames = 0;
                _pendingGroundState      = rawGrounded;
            }
            else if (rawGrounded == _pendingGroundState)
            {
                // Consistent with a pending change — count toward the threshold
                _groundStateStableFrames++;
                if (_groundStateStableFrames >= GroundedDebounceFrames)
                {
                    _groundInfo.OnGround     = rawGrounded;
                    _groundStateStableFrames = 0;
 
                    if (!rawGrounded)
                    {
                        _groundInfo.OnSlope    = false;
                        _groundInfo.SlopeAngle = 0f;
                    }
                }
            }
            else
            {
                // Flipped to a new candidate state — restart the count
                _pendingGroundState      = rawGrounded;
                _groundStateStableFrames = 1;
            }
        }
 
 
    }
}