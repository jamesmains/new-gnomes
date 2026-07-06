using System;
using GNOMES.Actor.Core;
using Gnomes.Demo.Modules;
using GNOMES.Input;
using GNOMES.Modules;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Demo.Scripts
{
    /// <summary>
    /// First-person style camera with smooth rotation, head bob, and tilt juice.
    /// Reads the Look intent from CameraModule directly.
    ///
    /// Requires: CameraModule on the brain (PlayerBrain only — silent on AI).
    /// Requires: LocomotionModule on the brain (for velocity-driven bob/tilt).
    ///
    /// BehaviourOrder(0) — runs after Movement3dBehavior(-100) so FacingDirection
    /// is already updated when the camera reads it.
    /// </summary>
    [Serializable]
    [RequiresModule(typeof(CameraModule))]
    [RequiresModule(typeof(MovementModule))]
    [BehaviourOrder(0)]
    public class DefaultCameraBehavior : CameraBehavior
    {
        // ── Settings ──────────────────────────────────────────────────────────
 
        [Header("Position")]
        public Vector3 Offset;
        public float   Distance;
        public Transform CameraPivot;
 
        [Header("Rotation")]
        public float Sensitivity = 0.5f;
        public float SmoothTime  = 0.03f;
 
        [Header("Head Bob")]
        public float BobSpeed     = 10f;
        public float BobAmount    = 0.05f;
        public float BobSmoothing = 1f;
 
        [Header("Tilt / Juice")]
        public float TiltAmount      = 2.0f;
        public float JuiceSmoothSpeed = 10f;
 
        // ── Runtime dependencies ──────────────────────────────────────────────
 
        private MovementModule _loco;
        private CameraModule _cameraModule;
 
        // ── Intent — resolved once in OnPostBind ──────────────────────────────
 
        private IntentValue<Vector2> _lookIntent;
 
        // ── Camera smoothing state ────────────────────────────────────────────
 
        private float   _targetYaw;
        private float   _targetPitch;
        private Vector3 _hardLookDir;
        private Vector3 _smoothLookDir;
        private Vector3 _followVelocity;
 
        // ── Bob state ─────────────────────────────────────────────────────────
 
        private float _bobCycle;
        private float _currentBobWeight;
 
        // ── Tilt state ────────────────────────────────────────────────────────
 
        private float _currentTilt;
 
        // ── Lifecycle ─────────────────────────────────────────────────────────
 
        protected override bool VerifyRequirements()
        {
            if (!base.VerifyRequirements()) {
                Debug.LogWarning(
                    $"[Gnomes] DefaultCameraBehavior is not meeting base requirements.");
                return false;
            }
 
            _loco = GetModule<MovementModule>();
            if (_loco == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] DefaultCameraBehavior on '{Parent?.name}': " +
                    "no MovementModule on brain.");
                return false;
            }
            
            _cameraModule = GetModule<CameraModule>();
            if (_cameraModule == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] DefaultCameraBehavior on '{Parent?.name}': " +
                    "no CameraModule on brain.");
                return false;
            }
            
            if (_cameraModule.Camera == null && Parent.Brain is PlayerBrain playerBrain) {
                if (playerBrain.Player == null) return false;
                _cameraModule.Camera = playerBrain.Player.PlayerCamera;
            }
 
            if (_cameraModule?.Camera == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] DefaultCameraBehavior on '{Parent?.name}': " +
                    "CameraModule has no Camera after OnPostBind.");
                return false;
            }
 
            return true;
        }
 
        protected override void OnPostBind()
        {
            base.OnPostBind();   // sets CameraModule.Camera and CameraPivot
 
            // Resolve Look intent from CameraModule — added by the Setup Wizard.
            // Null if the wizard hasn't run yet; camera simply receives no input
            // and stays at its seeded orientation.
            _lookIntent = ResolveIntent<Vector2>(_cameraModule, "Look");
 
            // Seed look direction from the actor's current facing so the camera
            // doesn't snap on first possession
            _hardLookDir   = Parent.transform.forward;
            _smoothLookDir = _hardLookDir;
 
            var euler  = Parent.transform.eulerAngles;
            _targetYaw   = euler.y;
            _targetPitch = 0f;
        }
 
        // ── Update — input and immediate direction ────────────────────────────
 
        public override void Update()
        {
            if (!IsValid) return;
 
            // Read Look intent — null-safe, zero if wizard hasn't mapped it yet
            Vector2 rawInput = _lookIntent?.Value ?? Vector2.zero;
 
            //_cameraModule.LookInput.Value = new Vector3(rawInput.x, rawInput.y, 0f);
 
            _targetYaw   += rawInput.x * Sensitivity;
            _targetPitch -= rawInput.y * Sensitivity;
            _targetPitch  = Mathf.Clamp(_targetPitch, -85f, 85f);
 
            // Hard direction — zero smoothing, used for physics/body rotation
            _hardLookDir = Quaternion.Euler(_targetPitch, _targetYaw, 0f) * Vector3.forward;
 
            _loco.FacingDirection.Value = _hardLookDir;
            //_loco.Pitch.Value           = _targetPitch;
        }
 
        // ── FixedUpdate — position and visual smoothing ───────────────────────
 
        public override void FixedUpdate()
        {
            // Silent early-out on AI actors — not an error
            if (!IsValid) return;
            if (_cameraModule?.Camera == null) return;
 
            var camTransform = _cameraModule.Camera.transform;
 
            // 1. Smooth look direction for visual rotation only
            _smoothLookDir = Vector3.SmoothDamp(
                _smoothLookDir, _hardLookDir,
                ref _followVelocity, SmoothTime);
 
            // Guard against zero-length on first frame
            if (_smoothLookDir.sqrMagnitude < 0.0001f)
                _smoothLookDir = _hardLookDir;
 
            // 2. Position — rigid lock to pivot with hard rotation
            // CameraModule.CameraPivot is set in CameraBehavior.OnPostBind
            // to Parent.CameraRoot or Parent.transform as fallback
            Vector3 worldOffset  = Parent.transform.TransformDirection(Offset);
            Vector3 pivotPoint   = CameraPivot.position + worldOffset;
            Vector3 basePosition = pivotPoint - (_hardLookDir * Distance);
 
            // 3. Head bob — driven by horizontal speed from LocomotionModule
            Vector3 bobOffset     = Vector3.zero;
            Vector3 flatVelocity  = new Vector3(
                _loco.Velocity.Value.x, 0f, _loco.Velocity.Value.z);
            float   horizontalSpeed = flatVelocity.magnitude;
 
            _bobCycle       += Time.fixedDeltaTime * BobSpeed * Mathf.Max(horizontalSpeed, 1f);
            _currentBobWeight = Mathf.Lerp(
                _currentBobWeight,
                horizontalSpeed > 0.1f ? 1f : 0f,
                Time.fixedDeltaTime * BobSmoothing);
 
            if (_currentBobWeight > 0.001f)
            {
                bobOffset.y = Mathf.Sin(_bobCycle)         * BobAmount        * _currentBobWeight;
                bobOffset.x = Mathf.Cos(_bobCycle * 0.5f) * BobAmount * 0.5f * _currentBobWeight;
            }
 
            // 4. Tilt — driven by lateral velocity (strafe feel)
            Vector3 localVelocity = Parent.transform.InverseTransformDirection(
                _loco.Velocity.Value);
            _currentTilt = Mathf.Lerp(
                _currentTilt,
                -localVelocity.x * TiltAmount,
                Time.fixedDeltaTime * JuiceSmoothSpeed);
 
            // 5. Final application
            // Position uses hard direction — no lag
            // Rotation uses smooth direction — visual silkiness
            camTransform.position = basePosition + bobOffset;
            camTransform.rotation = Quaternion.LookRotation(_smoothLookDir) *
                                    Quaternion.Euler(0f, 0f, _currentTilt);
 
            _loco.CurrentFacingDirection.Value = _smoothLookDir;
        }
 
        // ── Intent resolution helper ──────────────────────────────────────────
 
        private static IntentValue<T> ResolveIntent<T>(
            IBrainModule module, string fieldName)
        {
            if (module == null || string.IsNullOrEmpty(fieldName)) return null;
 
            for (var t = module.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);
 
                if (prop != null &&
                    typeof(IntentValue<T>).IsAssignableFrom(prop.PropertyType))
                    return prop.GetValue(module) as IntentValue<T>;
            }
 
            return null;
        }
    }
}