using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GNOMES.Actor.Core.Interaction {
    /// <summary>
    /// Place on any GameObject to make it interactable.
    ///
    /// Supports multiple named option sets — call ActivateSet("Held") to swap
    /// to a different set of options at runtime (e.g. Pick Up → Drop).
    /// The prompt updates automatically when the active set changes.
    ///
    /// Hover lifecycle events fire when an actor's raycast enters/exits.
    /// Subscribe for highlight/outline effects.
    /// </summary>
    public class GnomesInteractable : MonoBehaviour
    {
        // ── Option sets ───────────────────────────────────────────────────────
 
        [Tooltip("Named option sets. The first set named 'Default' is active on start.")]
        public List<InteractionOptionSet> OptionSets = new()
        {
            new InteractionOptionSet { Name = "Default" }
        };
 
        // ── Hover events ──────────────────────────────────────────────────────
 
        public event Action<Actor>  OnHoverEnter;
        public event Action<Actor>  OnHoverExit;
        public UnityEvent<Actor>    OnHoverEnterEvent;
        public UnityEvent<Actor>    OnHoverExitEvent;
 
        // ── Set change event ──────────────────────────────────────────────────
 
        /// <summary>Fired when the active option set changes.</summary>
        public event Action<InteractionOptionSet> OnSetChanged;
 
        // ── Runtime state ─────────────────────────────────────────────────────
 
        private InteractionOptionSet _activeSet;
 
        private void Awake()
        {
            // Activate the first set named "Default", or just the first set
            _activeSet = OptionSets.Find(s => s.Name == "Default")
                         ?? (OptionSets.Count > 0 ? OptionSets[0] : null);
        }
 
        // ── Public API ────────────────────────────────────────────────────────
 
        public InteractionOptionSet ActiveSet => _activeSet;
 
        /// <summary>
        /// Switches to the named option set and notifies the prompt.
        /// No-op if the set is already active or the name isn't found.
        /// </summary>
        public void ActivateSet(string setName)
        {
            var set = OptionSets.Find(s => s.Name == setName);
            if (set == null)
            {
                Debug.LogWarning(
                    $"[Gnomes] GnomesInteractable '{name}': " +
                    $"no option set named '{setName}'.");
                return;
            }
 
            if (_activeSet == set) return;
            _activeSet = set;
            OnSetChanged?.Invoke(_activeSet);
        }
 
        /// <summary>Returns all enabled options in the active set, sorted by priority.</summary>
        public List<InteractionOption> GetEnabledOptions()
        {
            var result = new List<InteractionOption>();
            if (_activeSet == null) return result;
 
            foreach (var opt in _activeSet.Options)
                if (opt.Enabled) result.Add(opt);
 
            result.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return result;
        }
 
        /// <summary>Triggers the highest-priority enabled option.</summary>
        public void TriggerPrimary(Actor actor)
        {
            var options = GetEnabledOptions();
            if (options.Count > 0) options[0].Trigger(actor);
        }
 
        /// <summary>Triggers the option at a specific index in the enabled options list.</summary>
        public void TriggerAtIndex(int index, Actor actor)
        {
            var options = GetEnabledOptions();
            if (index >= 0 && index < options.Count)
                options[index].Trigger(actor);
        }
 
        /// <summary>
        /// Enables or disables an option by label in the active set.
        /// Use for contextual availability (full inventory, locked etc.).
        /// </summary>
        public void SetOptionEnabled(string label, bool enabled)
        {
            if (_activeSet == null) return;
            foreach (var opt in _activeSet.Options)
                if (opt.Label == label) opt.Enabled = enabled;
        }
 
        // ── Internal hover notification ───────────────────────────────────────
 
        internal void NotifyHoverEnter(Actor actor)
        {
            OnHoverEnter?.Invoke(actor);
            OnHoverEnterEvent?.Invoke(actor);
        }
 
        internal void NotifyHoverExit(Actor actor)
        {
            OnHoverExit?.Invoke(actor);
            OnHoverExitEvent?.Invoke(actor);
        }
    }
}