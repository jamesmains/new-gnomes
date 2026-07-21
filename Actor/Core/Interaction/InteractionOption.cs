// ═════════════════════════════════════════════════════════════════════════════
//  GnomesInteraction.cs — Parent House Framework · Gnomes
//  Contains: InteractionOption, GnomesInteractable,
//            InteractionModule, InteractorBehavior,
//            GnomesInteractionPrompt
//
//  Design:
//  - GnomesInteractable  — component on any interactable object. Holds a list
//                          of InteractionOptions (icon, label, keybind, callback)
//  - InteractionModule   — brain module that stores the Interact intent trigger
//                          and the current hover target. Added to the brain via
//                          the Setup Wizard like any other module.
//  - InteractorBehavior  — ActorBehavior that raycasts each frame, manages
//                          hover state, and fires interactions on intent trigger.
//  - GnomesInteractionPrompt — UI MonoBehaviour that listens to hover state
//                          and renders icon + keybind pairs for each option.
//
//  Usage:
//  1. Add GnomesInteractable to any interactable GameObject.
//  2. Add InteractionOptions in the inspector (icon, label, keybind hint,
//     and wire up the UnityEvent or subscribe via OnInteract in code).
//  3. Add InteractionModule to the PlayerBrain via the brain inspector.
//  4. Map your "Interact" input action to InteractionModule via the Setup Wizard.
//  5. Add InteractorBehavior to the Actor's InitialBehaviors list.
//  6. (Optional) Add GnomesInteractionPrompt to a UI canvas and assign it.
// ═════════════════════════════════════════════════════════════════════════════

using System;
using UnityEngine;
using UnityEngine.Events;

namespace GNOMES.Actor.Core.Interaction
{
    // ── InteractionOption ─────────────────────────────────────────────────────

    /// <summary>
    /// A single interaction available on a GnomesInteractable.
    /// Each option has a display name, optional icon, keybind hint string,
    /// and a callback that fires when the player triggers it.
    ///
    /// Multiple options on the same interactable appear as a stacked prompt
    /// (e.g. "E  Examine" above "F  Pick Up").
    /// </summary>
    [Serializable]
    public class InteractionOption
    {
        [Tooltip("Short verb shown in the prompt, e.g. 'Pick Up', 'Examine', 'Open'.")]
        public string Label;

        [Tooltip("Optional icon shown alongside the label.")]
        public Sprite Icon;

        [Tooltip("Keybind hint shown in the prompt, e.g. 'E', 'F', 'LMB'. " +
                 "Cosmetic only — actual binding comes from your input asset.")]
        public string KeybindHint;

        [Tooltip("Priority determines which option fires when only one interact " +
                 "button is mapped. Higher value = fires first.")]
        public int Priority;

        [Tooltip("If true this option is shown and available. " +
                 "Toggle at runtime to hide options contextually.")]
        public bool Enabled = true;

        // ── Callbacks ─────────────────────────────────────────────────────────

        /// <summary>
        /// UnityEvent callback — wire up in the inspector without code.
        /// Receives the Actor that triggered the interaction.
        /// </summary>
        [Space(4)]
        public UnityEvent<Actor> OnInteract;

        /// <summary>
        /// Code-side callback — subscribe in Awake or Start.
        /// Alternative to OnInteract for cases where inspector wiring
        /// isn't convenient.
        /// </summary>
        [NonSerialized]
        public Action<Actor> OnInteractCallback;

        /// <summary>Called by InteractorBehavior when this option is triggered.</summary>
        internal void Trigger(Actor actor)
        {
            OnInteract?.Invoke(actor);
            OnInteractCallback?.Invoke(actor);
        }
    }
}