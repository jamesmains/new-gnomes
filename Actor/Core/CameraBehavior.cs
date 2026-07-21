using System;
using UnityEngine;

namespace GNOMES.Actor.Core {
    /// <summary>
    /// Categorized behavior for Camera exclusive functionality.
    /// Recommended use is to include a CameraModule of some kind on derived classes.
    /// Typical use-case would be to only be active when the parent actor has a PlayerBrain,
    /// included recommended functionality provided.
    /// </summary>
    [Serializable]
    public abstract class CameraBehavior : ActorBehavior {

        protected override bool VerifyRequirements() {
            if (Parent.Brain is PlayerBrain) return true;
        
            Debug.Log($"[Camera Behavior] Actor does not have a PlayerBrain : {Parent.gameObject.name}");
            return false;
        }

        protected override void OnPostBind() {
            if (Parent.Brain is not PlayerBrain) return;
        }
    }
}