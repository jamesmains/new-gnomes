using System.Collections.Generic;
using GNOMES.Actor.Core.Scene;
using UnityEngine;

namespace gnomes.Actor.Core.UI {
    public class GnomesUITrigger : MonoBehaviour {
        [Header("UI Config")] 
        [Tooltip("Name of the action map to switch to. Must match your .inputactions asset.")]
        public string UIActionMap = "UI"; 

        [Tooltip("Cursor behavior while UI is active.")]
        public GnomesCursorMode CursorMode = GnomesCursorMode.UnlockedVisible; 

        [Tooltip("The Screen-Space UI Canvas Prefab to spawn for the player.")]
        public Canvas UICanvasPrefab; 

        [Tooltip("Whether to auto-select the first selectable element for gamepad navigation.")]
        public bool AutoSelectFirst = true; 

        // Track spawned canvases per player index so we don't spawn duplicates
        private readonly Dictionary<int, Canvas> _spawnedPlayerCanvases = new();

        // ── Public API ────────────────────────────────────────────────────────

        public void Open(GNOMES.Actor.Core.Actor actor) {
            var slot = FindSlotForActor(actor); 
            if (slot == null) {
                Debug.LogWarning($"[Gnomes] GnomesUITrigger '{name}': no player slot found."); 
                return;
            }

            // 1. Get or Instantiate the player-isolated Canvas
            Canvas playerCanvas = GetOrCreateCanvasForPlayer(slot);
            if (playerCanvas == null) return;

            Debug.Log($"[GNOMES UI] Opening {gameObject.name} for {actor} in slot {slot.PlayerIndex}."); 

            // 2. Pass the dedicated instance to their context
            GnomesUIContext.For(slot).Enter(new GnomesUIConfig {
                UIActionMap = UIActionMap, 
                CursorMode = CursorMode, 
                WorldSpaceCanvas = playerCanvas, // Passing the player-owned instance
                AutoSelectFirstElement = AutoSelectFirst
            });
        }

        public void Close(GNOMES.Actor.Core.Actor actor) {
            var slot = FindSlotForActor(actor); 
            if (slot == null) return; 
            GnomesUIContext.For(slot).Exit(); 
        }

        public void CloseAll(GNOMES.Actor.Core.Actor actor) {
            var slot = FindSlotForActor(actor); 
            if (slot == null) return; 
            GnomesUIContext.For(slot).ExitAll(); 
        }

        // ── Player-Isolated Spawning Logic ────────────────────────────────────

        private Canvas GetOrCreateCanvasForPlayer(GnomesPlayerSlot slot) {
            if (UICanvasPrefab == null) {
                Debug.LogError($"[Gnomes] UICanvasPrefab is missing on trigger '{name}'!");
                return null;
            }

            // Check if this player already spawned this specific UI menu
            if (_spawnedPlayerCanvases.TryGetValue(slot.PlayerIndex, out var existingCanvas) && existingCanvas != null) {
                return existingCanvas;
            }

            // Spawn a fresh copy of the UI explicitly for this player
            Canvas spawnedCanvas = Instantiate(UICanvasPrefab);
            
            // Core Multi-UI Configuration:
            // Set render mode to camera and hook it up to the player's unique split-screen camera
            spawnedCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            
            // Note: Update 'slot.Player.Camera' to match wherever your project holds the player's Camera reference
            if (slot.Player != null && slot.Player.PlayerCamera != null) {
                spawnedCanvas.worldCamera = slot.Player.PlayerCamera;
                
                // FIX FOR DRAWING BEHIND OBJECTS:
                // 1. Move the UI plane incredibly close to the camera lens (e.g., 0.1 units)
                //    so no world objects can physically fit between the camera and the UI.
                spawnedCanvas.planeDistance = 1f; 
    
                // 2. Override the sorting order to ensure it draws over standard elements
                spawnedCanvas.sortingOrder = 100;
                
            } else {
                Debug.LogWarning($"[Gnomes] Player Slot {slot.PlayerIndex} is missing a Camera reference! Screen space overlay may not clip correctly.");
            }

            // Route Unity's MultiplayerEventSystem to only look at this canvas branch
            var multiEventSystem = slot.Player?.EventSystem;
            if (multiEventSystem != null) {
                multiEventSystem.playerRoot = spawnedCanvas.gameObject;
            }

            _spawnedPlayerCanvases[slot.PlayerIndex] = spawnedCanvas;
            return spawnedCanvas;
        }

        private static GnomesPlayerSlot FindSlotForActor(GNOMES.Actor.Core.Actor actor) {
            if (actor == null) return null; 
            foreach (var slot in GnomesSceneManager.Instance.PlayerSlots) 
                if (slot.CurrentActor == actor) 
                    return slot; 
            return null; 
        }

        // Clean up UI instances if the trigger object is destroyed or player leaves
        private void OnDestroy() {
            foreach (var canvas in _spawnedPlayerCanvases.Values) {
                if (canvas != null) Destroy(canvas.gameObject);
            }
            _spawnedPlayerCanvases.Clear();
        }
    }
}