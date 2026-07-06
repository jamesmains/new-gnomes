// ═════════════════════════════════════════════════════════════════════════════
//  GnomesInspect.cs — Parent House Framework · Gnomes
//  Contains: GnomesInspectPoint, GnomesInspectBehavior
//
//  Design:
//  - GnomesInspectPoint  — component on the inspectable object. Configures
//                          the inspect brain, camera, and exit conditions.
//                          Auto-wires interaction options on the sibling
//                          GnomesInteractable at startup.
//
//  - GnomesInspectBehavior — ActorBehavior on the actor. Tracks inspection
//                          state, runs the exit radius check, and handles
//                          revert on exit. Lives dormant when not inspecting.
//
//  How it fits into the existing system:
//  - Entry: GnomesInteractable fires "Inspect" → GnomesInspectPoint.Enter()
//           → actor.SwapBrain(InspectBrain). All existing behavior lifecycle
//           (Unbind/Bind, module reset) happens automatically via SwapBrain.
//  - Exit via prompt: "Exit Inspect" InteractionOption fires
//           GnomesInspectPoint.Exit() → actor.RevertToDefault()
//  - Exit via radius: GnomesInspectBehavior checks distance each Update,
//           calls Exit() automatically when ExitRadius is exceeded.
//  - Exit via timeout: optional, same path as radius.
//
//  The InspectBrain asset should have:
//  - CameraModule (point InspectCamera at your inspection target)
//  - No LocomotionModule needed (actor doesn't move during inspection)
//  - Optionally an InteractionModule if you want in-inspection interactions
// ═════════════════════════════════════════════════════════════════════════════

using System;
using UnityEngine;
using UnityEngine.Events;

namespace GNOMES.Actor.Core.Interaction
{
    // ── GnomesInspectPoint ────────────────────────────────────────────────────

    /// <summary>
    /// Add to any inspectable object alongside a GnomesInteractable.
    /// Configures the brain, camera, and exit conditions for inspection.
    ///
    /// On Awake, automatically adds:
    ///   - An "Inspect" option to the interactable's "Default" set
    ///   - An "Inspecting" option set with an "Exit Inspect" option
    ///
    /// The actor's GnomesInspectBehavior handles the actual brain swap
    /// and exit radius check. This component is purely configuration + wiring.
    ///
    /// Example uses:
    ///   - Security camera terminal (possess camera brain, exit with E)
    ///   - Item inspection (orbit camera around object, exit with E)
    ///   - Painting/sign reading (close-up camera, auto-exit on walk away)
    ///   - Dialogue terminal (fixed camera angle, exit after dialogue)
    /// </summary>
    public class GnomesInspectPoint : MonoBehaviour
    {
        // ── Settings ──────────────────────────────────────────────────────────

        [Header("Brain")]
        [Tooltip("The ActorBrain to swap to when entering inspection. " +
                 "Should have a CameraModule with InspectCamera assigned. " +
                 "A new instance is created per inspection so shared assets " +
                 "are safe to use across multiple inspect points.")]
        public Actor InspectableActor;

        [Header("Inspect Option")]
        [Tooltip("Label shown in the interaction prompt for entering inspection.")]
        public string InspectLabel    = "Inspect";

        [Tooltip("Keybind hint shown in the prompt for entering inspection.")]
        public string InspectKeybind  = "E";

        [Tooltip("Icon for the inspect prompt option. Optional.")]
        public Sprite InspectIcon;

        [Header("Exit Option")]
        [Tooltip("Label shown in the interaction prompt for exiting inspection.")]
        public string ExitLabel   = "Exit";

        [Tooltip("Keybind hint shown in the prompt for exiting inspection.")]
        public string ExitKeybind = "E";

        [Tooltip("Icon for the exit prompt option. Optional.")]
        public Sprite ExitIcon;

        [Header("Exit Conditions")]
        [Tooltip("Distance from this object at which the actor automatically " +
                 "exits inspection. 0 = disabled.")]
        public float ExitRadius = 0f;

        [Tooltip("Time in seconds before the actor is automatically returned. " +
                 "0 = no timeout.")]
        public float ExitTimeout = 0f;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when an actor begins inspecting this point.</summary>
        public event Action<Actor> OnInspectEntered;

        /// <summary>Fired when an actor exits inspection of this point.</summary>
        public event Action<Actor> OnInspectExited;

        public UnityEvent<Actor> OnInspectEnteredEvent;
        public UnityEvent<Actor> OnInspectExitedEvent;

        // ── Runtime ───────────────────────────────────────────────────────────

        private GnomesInteractable _interactable;
        private Actor              _inspectingActor;
        private ActorBrain         _inspectingActorBrain;
        private bool               _isBeingInspected;

        public bool IsBeingInspected => _isBeingInspected;
        public Actor InspectingActor => _inspectingActor;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _interactable = GetComponent<GnomesInteractable>();

            if (_interactable == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] GnomesInspectPoint on '{name}' has no sibling " +
                    "GnomesInteractable. Add one for the prompt to work.");
                return;
            }

            WireOptions();
        }

        private void OnDisable()
        {
            // If inspection is active and this object is disabled, exit cleanly
            if (_isBeingInspected && _inspectingActor != null)
                ExitInspection(_inspectingActor);
        }

        // ── Option wiring ─────────────────────────────────────────────────────

        private void WireOptions()
        {
            // ── "Inspect" option on the Default set ───────────────────────────
            var defaultSet = _interactable.OptionSets.Find(s => s.Name == "Default");
            if (defaultSet == null)
            {
                defaultSet = new InteractionOptionSet { Name = "Default" };
                _interactable.OptionSets.Insert(0, defaultSet);
            }

            // Avoid adding duplicate options if Awake fires multiple times
            if (!defaultSet.Options.Exists(o => o.Label == InspectLabel))
            {
                var inspectOption = new InteractionOption
                {
                    Label       = InspectLabel,
                    KeybindHint = InspectKeybind,
                    Icon        = InspectIcon,
                    Priority    = 100,   // appears above other options
                    Enabled     = true
                };
                inspectOption.OnInteractCallback += EnterInspection;
                defaultSet.Options.Add(inspectOption);
            }

            // ── "Inspecting" set with Exit option ─────────────────────────────
            var inspectingSet = _interactable.OptionSets.Find(s => s.Name == "Inspecting");
            if (inspectingSet == null)
            {
                inspectingSet = new InteractionOptionSet { Name = "Inspecting" };
                _interactable.OptionSets.Add(inspectingSet);
            }

            if (!inspectingSet.Options.Exists(o => o.Label == ExitLabel))
            {
                var exitOption = new InteractionOption
                {
                    Label       = ExitLabel,
                    KeybindHint = ExitKeybind,
                    Icon        = ExitIcon,
                    Priority    = 100,
                    Enabled     = true
                };
                exitOption.OnInteractCallback += ExitInspection;
                inspectingSet.Options.Add(exitOption);
            }
        }

        // ── Enter / Exit ──────────────────────────────────────────────────────

        private void EnterInspection(Actor actor) {
            if (actor.Brain == null || actor.Brain is not PlayerBrain playerBrain) return;
            if (InspectableActor == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] GnomesInspectPoint '{name}': no InspectBrain assigned.");
                return;
            }

            if (_isBeingInspected)
            {
                Debug.LogWarning(
                    $"[Gnomes] GnomesInspectPoint '{name}' is already being " +
                    "inspected. Only one actor can inspect at a time.");
                return;
            }

            _isBeingInspected = true;
            _inspectingActor  = actor;
            
            // Swap brain — all existing lifecycle fires automatically
            InspectableActor.SwapBrain(playerBrain);

            // Notify the actor's GnomesInspectBehavior before the swap
            // so it can set up exit condition tracking
            var inspectBehavior = GetInspectBehavior(InspectableActor);
            inspectBehavior?.OnInspectionStarted(this, playerBrain);


            _inspectingActorBrain = playerBrain;

            // Switch interactable to inspecting set so prompt shows "Exit"
            _interactable.ActivateSet("Inspecting");

            OnInspectEntered?.Invoke(actor);
            OnInspectEnteredEvent?.Invoke(actor);
        }

        internal void ExitInspection(Actor actor)
        {
            if (!_isBeingInspected || actor != InspectableActor) return;

            var inspectBehavior = GetInspectBehavior(InspectableActor);
            inspectBehavior?.HandleOnInspectionEnded();

            // RevertToDefault destroys the inspect brain instance and
            // re-attaches the actor's original brain — full lifecycle fires
            _inspectingActor.SwapBrain(_inspectingActorBrain);

            _interactable.ActivateSet("Default");

            OnInspectExited?.Invoke(InspectingActor);
            OnInspectExitedEvent?.Invoke(InspectingActor);
            
            _isBeingInspected = false;
            _inspectingActor  = null;
            _inspectingActorBrain = null;
        }

        private static GnomesInspectBehavior GetInspectBehavior(Actor actor)
        {
            actor.GetBehavior<GnomesInspectBehavior>(out var b);
            return b;
        }
    }
}