using System;
using System.Collections.Generic;
using System.Linq;
using GNOMES.Actor.Core;
using GNOMES.Actor.Core.Scene;
using UnityEngine;
using UnityEngine.InputSystem.UI; // Needed for MultiplayerEventSystem configuration

namespace gnomes.Actor.Core.UI {
    public class GnomesUITrigger : MonoBehaviour {
        [Header("UI Config")] 
        [Tooltip("Name of the action map to switch to. Must match your .inputactions asset.")]
        public string UIActionMap = "UI"; //[cite: 4]

        [Tooltip("Cursor behavior while UI is active.")]
        public GnomesCursorMode CursorMode = GnomesCursorMode.UnlockedVisible; //[cite: 4]

        [Tooltip("The Screen-Space UI Canvas Prefab to spawn for the player.")]
        public Canvas UICanvasPrefab; 

        [Tooltip("Whether to auto-select the first selectable element for gamepad navigation.")]
        public bool AutoSelectFirst = true; //[cite: 4]

        [Header("Behaviors to Freeze")]
        [Tooltip("Names of behavior types to freeze while UI is active.")]
        public List<string> FreezeBehaviorTypeNames = new() {
            "Movement3dBehavior",
            "DefaultCameraBehavior"
        }; //[cite: 4]

        // Track spawned canvases per player index so we don't spawn duplicates
        private readonly Dictionary<int, Canvas> _spawnedPlayerCanvases = new();

        // ── Public API ────────────────────────────────────────────────────────

        public void Open(GNOMES.Actor.Core.Actor actor) {
            var slot = FindSlotForActor(actor); //[cite: 4]
            if (slot == null) {
                Debug.LogWarning($"[Gnomes] GnomesUITrigger '{name}': no player slot found."); //[cite: 4]
                return;
            }

            // 1. Get or Instantiate the player-isolated Canvas
            Canvas playerCanvas = GetOrCreateCanvasForPlayer(slot);
            if (playerCanvas == null) return;

            Debug.Log($"[GNOMES UI] Opening {gameObject.name} for {actor} in slot {slot.PlayerIndex}."); //[cite: 4]
            var types = ResolveTypes(FreezeBehaviorTypeNames); //[cite: 4]

            // 2. Pass the dedicated instance to their context
            GnomesUIContext.For(slot).Enter(new GnomesUIConfig {
                UIActionMap = UIActionMap, //[cite: 4]
                CursorMode = CursorMode, //[cite: 4]
                WorldSpaceCanvas = playerCanvas, // Passing the player-owned instance
                AutoSelectFirstElement = AutoSelectFirst, //[cite: 4]
                FreezeBehaviorTypes = types //[cite: 4]
            });
        }

        public void Close(GNOMES.Actor.Core.Actor actor) {
            var slot = FindSlotForActor(actor); //[cite: 4]
            if (slot == null) return; //[cite: 4]
            GnomesUIContext.For(slot).Exit(); //[cite: 4]
        }

        public void CloseAll(GNOMES.Actor.Core.Actor actor) {
            var slot = FindSlotForActor(actor); //[cite: 4]
            if (slot == null) return; //[cite: 4]
            GnomesUIContext.For(slot).ExitAll(); //[cite: 4]
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
            var multiEventSystem = slot.Player?.EventSystem as MultiplayerEventSystem;
            if (multiEventSystem != null) {
                multiEventSystem.playerRoot = spawnedCanvas.gameObject;
            }

            _spawnedPlayerCanvases[slot.PlayerIndex] = spawnedCanvas;
            return spawnedCanvas;
        }

        private static GnomesPlayerSlot FindSlotForActor(GNOMES.Actor.Core.Actor actor) {
            if (actor == null) return null; //[cite: 4]
            foreach (var slot in GnomesSceneManager.Instance.PlayerSlots) //[cite: 4]
                if (slot.CurrentActor == actor) //[cite: 4]
                    return slot; //[cite: 4]
            return null; //[cite: 4]
        }

        private static Type[] ResolveTypes(List<string> names) {
            if (names == null || names.Count == 0) return Array.Empty<Type>(); //[cite: 4]
            var result = new List<Type>(); //[cite: 4]
            foreach (var name in names) { //[cite: 4]
                Type found = null; //[cite: 4]
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { //[cite: 4]
                    found = assembly.GetTypes().FirstOrDefault(t => t.Name == name && typeof(ActorBehavior).IsAssignableFrom(t)); //[cite: 4]
                    if (found != null) break; //[cite: 4]
                }
                if (found != null) result.Add(found); //[cite: 4]
                else Debug.LogWarning($"[Gnomes] GnomesUITrigger: behavior type '{name}' not found."); //[cite: 4]
            }
            return result.ToArray(); //[cite: 4]
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