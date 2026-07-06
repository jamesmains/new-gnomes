using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace GNOMES.Actor.Core {
    public class Player : MonoBehaviour {
        [Header("Dependencies")] public Camera PlayerCamera;

        public MultiplayerEventSystem EventSystem;

        public PlayerInput PlayerInput;

        private void Awake() {
            PlayerInput = GetComponent<PlayerInput>();
            if (PlayerInput == null)
                Debug.LogError($"[Gnomes] Player '{name}' has no PlayerInput component.");
            DontDestroyOnLoad(this.gameObject);
        }
    }
}