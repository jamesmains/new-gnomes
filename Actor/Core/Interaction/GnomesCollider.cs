using System;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core.Interaction {
    public abstract class GnomesDataPacket {
        public abstract void GetCollisionData(GnomesColliderActor other, GnomesColliderActor self);
        public abstract void SendData(BehaviorCommunicator communicator);
    }

    public class GnomesActorDataPacket: GnomesDataPacket {
        public override void GetCollisionData(GnomesColliderActor other, GnomesColliderActor self) {
            throw new NotImplementedException();
        }

        public override void SendData(BehaviorCommunicator communicator) {
            throw new NotImplementedException();
        }
    }

    public struct GnomesColliderActor {
        public BehaviorCommunicator Communicator;
        public Actor Parent;
    }
    
    public class GnomesCollider: MonoBehaviour {
	    public Collider Collider;
	    public Collider2D Collider2d;
        [SerializeReference, Gnomeable]
        public GnomesDataPacket Data;

        private GnomesColliderActor SelfActor = new();
        private GnomesColliderActor OtherActor;

        private void Awake() {
            if(TryGetComponent<Collider>(out var collider)) {
                Collider = collider;
            }
            
            if(TryGetComponent<Collider2D>(out var collider2d)) {
                Collider2d = collider2d;
            }
            
            if(TryGetComponent<BehaviorCommunicator>(out var communicator)) {
                SelfActor.Communicator = communicator;
            }

            if (TryGetComponent<Actor>(out var actor)) {
                SelfActor.Parent = actor;
            }
        }

        private void OnTriggerEnter(Collider other) {
            if (!Collider || !TryGetComponent<Actor>(out var otherActor)) return;
        }

        private void OnTriggerExit(Collider other) {
            if (!Collider || !TryGetComponent<Actor>(out var otherActor)) return;
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!Collider2d || !TryGetComponent<Actor>(out var otherActor)) return;
        }

        private void OnTriggerExit2D(Collider2D other) {
            if (!Collider2d || !TryGetComponent<Actor>(out var otherActor)) return;
        }

        private void HandleSendData() {
            
        }
    }
}