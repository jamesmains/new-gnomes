using GNOMES.Actor.Core;
using Gnomes.Demo.Modules;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Behaviors {
    [RequiresModule(typeof(CameraModule))]
    public class CameraSimpleFollowBehavior : CameraBehavior {
        public float MoveSpeed;
        public Vector3 Offset;
        [SerializeField, SerializeReference, Gnomeable]
        private CameraModule _interactionCameraModule;

        protected override bool VerifyRequirements() {
            _interactionCameraModule = Parent.Brain.GetModule<CameraModule>();
            if (_interactionCameraModule == null || Parent.Brain == null ) return false;
            
            if (_interactionCameraModule.Camera == null && Parent.Brain is PlayerBrain playerBrain) {
                if (playerBrain.Player == null) return false;
                _interactionCameraModule.Camera = playerBrain.Player.PlayerCamera;
            }

            return base.VerifyRequirements() && _interactionCameraModule.Camera != null;
        }

        public override void FixedUpdate() {
            if (!IsValid) return;
            base.FixedUpdate();
            var targetPosition = Parent.transform.position - Offset;
            var cameraCurrentPosition = _interactionCameraModule.Camera.transform.position;
            _interactionCameraModule.Camera.transform.position =
                Vector3.Lerp(cameraCurrentPosition, targetPosition, MoveSpeed * Time.deltaTime);
            _interactionCameraModule.Camera.transform.LookAt(Parent.transform);
        }
    }
}