using System;
using System.Collections.Generic;
using GNOMES.Actor.Core;
using GNOMES.Actor.Core.Scene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace gnomes.Actor.Core.UI {
    /// <summary>
    /// Per-player UI interaction context. Manages the mechanical handoff
    /// between gameplay and UI — input maps, cursor state, behavior freezing,
    /// and canvas activation — without owning any UI visual logic.
    ///
    /// One context is maintained per player slot. Access via GnomesUIContext.For().
    ///
    /// Stacking: Enter() can be called while already in UI mode (e.g. opening
    /// a sub-menu from a menu). Each Enter() pushes a new config onto a stack;
    /// Exit() pops back to the previous config. ExitAll() collapses the stack
    /// back to gameplay state in one call.
    /// </summary>
    public class GnomesUIContext {
        // ── Per-slot registry ─────────────────────────────────────────────────

        private static readonly Dictionary<int, GnomesUIContext> _contexts = new();

        /// <summary>
        /// Gets or creates the UI context for a player slot.
        /// PlayerIndex matches GnomesPlayerSlot.PlayerIndex.
        /// </summary>
        public static GnomesUIContext For(GnomesPlayerSlot slot) =>
            For(slot.PlayerIndex, slot);

        /// <summary>
        /// Gets or creates the UI context for a player index.
        /// Provide the slot for first-time creation; subsequent calls
        /// with only the index return the existing context.
        /// </summary>
        public static GnomesUIContext For(int playerIndex,
            GnomesPlayerSlot slot = null) {
            if (!_contexts.TryGetValue(playerIndex, out var ctx)) {
                ctx = new GnomesUIContext(playerIndex, slot);
                _contexts[playerIndex] = ctx;
            }
            else if (slot != null) {
                // Update the slot reference in case the actor was respawned
                ctx._slot = slot;
            }

            return ctx;
        }

        /// <summary>Removes the context for a player slot — call on player leave.</summary>
        public static void Remove(int playerIndex) =>
            _contexts.Remove(playerIndex);

        // ── State ─────────────────────────────────────────────────────────────

        private int _playerIndex;
        private GnomesPlayerSlot _slot;

        // Config stack — supports nested UI contexts (menus within menus)
        private readonly Stack<GnomesUIConfig> _configStack = new();

        // Saved gameplay state restored on Exit
        private string _savedActionMap;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;

        // Behaviors frozen by the current top-of-stack config
        private readonly List<ActorBehavior> _frozenBehaviors = new();

        // Canvas activated by the current top-of-stack config
        private Canvas _activeCanvas;

        // ── Properties ────────────────────────────────────────────────────────

        public bool IsInUI => _configStack.Count > 0;
        public int Depth => _configStack.Count;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when UI mode is entered (stack was previously empty).</summary>
        public event Action<GnomesUIConfig> OnEntered;

        /// <summary>Fired when UI mode is fully exited (stack is now empty).</summary>
        public event Action OnExited;

        /// <summary>Fired on every push/pop including nested contexts.</summary>
        public event Action<GnomesUIConfig, bool> OnContextChanged;

        // ── Constructor ───────────────────────────────────────────────────────

        private GnomesUIContext(int playerIndex, GnomesPlayerSlot slot) {
            _playerIndex = playerIndex;
            _slot = slot;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Enters UI mode with the given config. If already in UI mode,
        /// pushes a new layer (nested context). The previous context is
        /// restored when this one exits.
        /// </summary>
        public void Enter(GnomesUIConfig config = null) {
            config ??= new GnomesUIConfig();

            bool wasEmpty = _configStack.Count == 0;

            if (wasEmpty) {
                // Save the current gameplay state before any changes
                SaveGameplayState();
            }
            else {
                // Deactivate the current top canvas before pushing a new context
                SetCanvasActive(_activeCanvas, false);
                UnfreezeBehaviors();
            }

            _configStack.Push(config);
            ApplyConfig(config);

            if (wasEmpty)
                OnEntered?.Invoke(config);

            OnContextChanged?.Invoke(config, true);
        }

        /// <summary>
        /// Exits the current UI context layer. If this was the last layer,
        /// fully restores gameplay state. If nested, returns to the previous
        /// UI config.
        /// </summary>
        public void Exit() {
            if (_configStack.Count == 0) {
                Debug.LogWarning(
                    $"[Gnomes] GnomesUIContext (P{_playerIndex}): " +
                    "Exit called but no UI context is active.");
                return;
            }

            var exiting = _configStack.Pop();

            // Deactivate what the exiting config activated
            SetCanvasActive(_activeCanvas, false);
            UnfreezeBehaviors();

            if (_configStack.Count == 0) {
                // Back to gameplay — restore all saved state
                RestoreGameplayState(exiting);
                OnExited?.Invoke();
            }
            else {
                // Returning to a previous UI layer
                var previous = _configStack.Peek();
                ApplyConfig(previous);
            }

            OnContextChanged?.Invoke(exiting, false);
        }

        /// <summary>
        /// Collapses all UI context layers and returns to gameplay in one call.
        /// Use when a single action should dismiss all nested menus at once
        /// (e.g. "Take control" button while in a sub-menu).
        /// </summary>
        public void ExitAll() {
            if (_configStack.Count == 0) return;

            SetCanvasActive(_activeCanvas, false);
            UnfreezeBehaviors();

            var last = _configStack.Peek();
            _configStack.Clear();

            RestoreGameplayState(last);
            OnExited?.Invoke();
            OnContextChanged?.Invoke(last, false);
        }

        // ── Apply / restore ───────────────────────────────────────────────────

        private void ApplyConfig(GnomesUIConfig config) {
            // ── 1. Input action map ───────────────────────────────────────────
            if (!string.IsNullOrEmpty(config.UIActionMap))
                SwitchActionMap(config.UIActionMap);

            // ── 2. Cursor ─────────────────────────────────────────────────────
            ApplyCursorMode(config.CursorMode);

            // ── 3. Freeze behaviors ───────────────────────────────────────────
            FreezeBehaviors(config.FreezeBehaviorTypes);

            // ── 4. Canvas ─────────────────────────────────────────────────────
            _activeCanvas = config.WorldSpaceCanvas;
            SetCanvasActive(_activeCanvas, true);

            // ── 5. Auto-select first UI element for gamepad navigation ─────────
            if (config.AutoSelectFirstElement && _activeCanvas != null)
                AutoSelectFirst(_activeCanvas);
        }

        private void SaveGameplayState() {
            _savedActionMap = GetCurrentActionMap();

            // Only capture hardware cursor state if this is the only player (Singleplayer fallback)
            if (IsSinglePlayer()) {
                _savedLockMode = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
            }
        }

        private void RestoreGameplayState(GnomesUIConfig lastConfig) {
            if (!string.IsNullOrEmpty(_savedActionMap))
                SwitchActionMap(_savedActionMap);

            // Only restore hardware cursor if singleplayer
            if (IsSinglePlayer()) {
                Cursor.lockState = _savedLockMode;
                Cursor.visible = _savedCursorVisible;
            }

            if (lastConfig.ResetEventSystemOnExit)
                ResetEventSystem();
        }

        // ── Action map helpers ────────────────────────────────────────────────

        private void SwitchActionMap(string mapName) {
            var input = GetPlayerInput();
            if (input == null) return;

            if (input.actions.FindActionMap(mapName) == null) {
                Debug.LogWarning(
                    $"[Gnomes] GnomesUIContext (P{_playerIndex}): " +
                    $"action map '{mapName}' not found in InputActionAsset.");
                return;
            }

            input.SwitchCurrentActionMap(mapName);
        }

        private string GetCurrentActionMap() =>
            GetPlayerInput()?.currentActionMap?.name ?? "";

        // ── Cursor helpers ────────────────────────────────────────────────────

        private void ApplyCursorMode(GnomesCursorMode mode) {
            // OPTION A: If it's local multiplayer, bypass the hardware cursor entirely.
            // Let MultiplayerEventSystem handle gamepad navigation naturally without moving an OS mouse.
            if (!IsSinglePlayer()) {
                // (Optional) If you have a custom software cursor script on the player UI, trigger it here:
                // GetPlayerVirtualCursor()?.SetVisible(mode != GnomesCursorMode.LockedHidden);
                return;
            }

            // Fallback for single-player hardware mouse control
            switch (mode) {
                case GnomesCursorMode.LockedHidden:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;
                case GnomesCursorMode.UnlockedVisible:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
                case GnomesCursorMode.ConfinedVisible:
                    Cursor.lockState = CursorLockMode.Confined;
                    Cursor.visible = true;
                    break;
            }
        }

        // ── Behavior freeze helpers ───────────────────────────────────────────

        private void FreezeBehaviors(Type[] types) {
            _frozenBehaviors.Clear();
            if (types == null || types.Length == 0) return;

            var actor = _slot?.CurrentActor;
            if (actor == null) return;

            foreach (var type in types) {
                // GetBehavior<T> requires a generic — we use reflection here
                // because the types are runtime values, not compile-time generics.
                // This is called only on Enter, never per frame, so the cost
                // is acceptable.
                var behavior = FindBehaviorByType(actor, type);
                if (behavior == null) continue;

                // Todo: Reimplement freeze
                //behavior.Freeze();
                _frozenBehaviors.Add(behavior);
            }
        }

        private void UnfreezeBehaviors() {
            // Todo: Reimplement freeze
            // foreach (var b in _frozenBehaviors)
            // b?.Unfreeze();
            _frozenBehaviors.Clear();
        }

        private static ActorBehavior FindBehaviorByType(GNOMES.Actor.Core.Actor actor, Type type) {
            // Walk the actor's active behaviors via reflection since GetBehavior<T>
            // is generic and we have a runtime Type instead
            var field = typeof(GNOMES.Actor.Core.Actor).GetField("ActiveBehaviors",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field?.GetValue(actor) is not List<ActorBehavior> behaviors)
                return null;

            foreach (var b in behaviors)
                if (b != null && type.IsInstanceOfType(b))
                    return b;

            return null;
        }

        // ── Canvas helpers ────────────────────────────────────────────────────

        private static void SetCanvasActive(Canvas canvas, bool active) {
            if (canvas == null) return;

            canvas.gameObject.SetActive(active);

            // Enable/disable the GraphicRaycaster so pointer events only
            // flow when the canvas is meant to be interactive
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = active;
        }

        private void AutoSelectFirst(Canvas canvas) {
            // Find the first active selectable in the canvas and tell the
            // player's EventSystem to select it — this is what enables
            // gamepad d-pad/stick navigation to work immediately without
            // the player having to hover a button first
            var selectable = canvas.GetComponentInChildren<
                UnityEngine.UI.Selectable>(includeInactive: false);

            if (selectable == null) return;

            var eventSystem = GetEventSystem();
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        // ── EventSystem helpers ───────────────────────────────────────────────

        private void ResetEventSystem() {
            var eventSystem = GetEventSystem();
            if (eventSystem == null) return;

            eventSystem.SetSelectedGameObject(null);
        }

        private EventSystem GetEventSystem() {
            // Use the player's own MultiplayerEventSystem — this is what
            // scopes pointer events per-player in split-screen correctly.
            // Falls back to the global EventSystem if not available.
            var player = _slot?.Player;
            if (player?.EventSystem != null)
                return player.EventSystem;

            return EventSystem.current;
        }

        // ── Slot helpers ──────────────────────────────────────────────────────

        private PlayerInput GetPlayerInput() =>
            _slot?.Player?.PlayerInput;

        // Helper to determine if we need to worry about global cursor hijacking
        private bool IsSinglePlayer() {
            // Check your SceneManager or InputSystem to see how many players are active
            if (GNOMES.Actor.Core.Scene.GnomesSceneManager.Instance?.PlayerSlots == null) return true;
            return GNOMES.Actor.Core.Scene.GnomesSceneManager.Instance.PlayerSlots.Count <= 1;
        }
    }
}