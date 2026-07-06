// ═════════════════════════════════════════════════════════════════════════════
//  GnomesUIContext.cs — Parent House Framework · Gnomes
//  Contains: GnomesUIConfig, GnomesUIContext
//
//  Design:
//  - GnomesUIContext is a lightweight per-player coordinator that manages
//    the three-way handoff between gameplay and UI interaction:
//    1. Input action map switching (gameplay ↔ UI)
//    2. Cursor lock state (locked/hidden ↔ unlocked/visible)
//    3. Behavior freezing (movement, look — whatever the caller specifies)
//
//  - The framework owns the mechanical handoff. The actual UI (Canvas,
//    Buttons, layout) is entirely the developer's responsibility.
//
//  - Supports N simultaneous players in split-screen via per-slot instances.
//    Each player slot gets its own GnomesUIContext so UI state is never
//    shared between players.
//
//  - Works with both mouse/pointer input and gamepad navigation. Unity's
//    MultiplayerEventSystem (already on each Player) scopes pointer events
//    per-player automatically once the context is entered.
//
//  Usage:
//    // Enter UI mode for a specific player
//    var ctx = GnomesUIContext.For(playerSlot);
//    ctx.Enter(new GnomesUIConfig
//    {
//        UIActionMap           = "UI",
//        FreezeBehaviorTypes   = new[] { typeof(Movement3dBehavior),
//                                        typeof(DefaultCameraBehavior) },
//        CursorMode            = GnomesCursorMode.UnlockedVisible,
//        WorldSpaceCanvas      = monitorCanvas
//    });
//
//    // Exit from a button callback
//    GnomesUIContext.For(playerSlot).Exit();
//
//  The computer monitor scenario:
//    Socket "Plug In" option triggers → ctx.Enter() with monitor canvas
//    "Return to cable" button → ctx.Exit() + retarget inspect
//    "Take control" button  → ctx.Exit() + brain swap
//    Unplug interaction     → ctx.Exit() + brain swap
// ═════════════════════════════════════════════════════════════════════════════

using System;
using UnityEngine;

namespace gnomes.Actor.Core.UI {
    /// <summary>
    /// Configuration passed to GnomesUIContext.Enter(). Defines what changes
    /// when UI mode is entered and what to restore when it exits.
    ///
    /// All fields are optional — only the ones you set take effect.
    /// </summary>
    public class GnomesUIConfig {
        /// <summary>
        /// Name of the action map to switch to on Enter.
        /// Typically "UI". Must exist in the player's InputActionAsset.
        /// On Exit, the previous action map is automatically restored.
        /// </summary>
        public string UIActionMap = "UI";

        /// <summary>
        /// Cursor behavior during UI interaction.
        /// On Exit, the previous cursor state is automatically restored.
        /// </summary>
        public GnomesCursorMode CursorMode = GnomesCursorMode.UnlockedVisible;

        /// <summary>
        /// Types of ActorBehavior to freeze on Enter and unfreeze on Exit.
        /// The context finds these by type on the player's current actor.
        ///
        /// Example:
        ///   FreezeBehaviorTypes = new[]
        ///   {
        ///       typeof(Movement3dBehavior),
        ///       typeof(DefaultCameraBehavior)
        ///   }
        /// </summary>
        public Type[] FreezeBehaviorTypes = Array.Empty<Type>();

        /// <summary>
        /// Optional world-space canvas to enable on Enter and disable on Exit.
        /// The canvas's GraphicRaycaster is activated so pointer events work.
        /// If null, no canvas management is performed — useful when the UI
        /// is screen-space or managed externally.
        /// </summary>
        public Canvas WorldSpaceCanvas;

        /// <summary>
        /// If true, the context selects the first selectable UI element in
        /// WorldSpaceCanvas on Enter. This enables gamepad navigation
        /// immediately without requiring a manual first-select.
        /// </summary>
        public bool AutoSelectFirstElement = true;

        /// <summary>
        /// If true, the context disables the player's MultiplayerEventSystem
        /// on Exit and re-enables it on Enter, preventing stale hover states
        /// on UI elements after the context closes.
        /// </summary>
        public bool ResetEventSystemOnExit = true;
    }
}