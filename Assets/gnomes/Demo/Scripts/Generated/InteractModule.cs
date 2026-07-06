// Created by Gnomes Setup Wizard
// Add your state fields below the WIZARD-END marker.
// Do not edit between the WIZARD markers manually.

using System;
using GNOMES.Actor.Core.Interaction;
using GNOMES.Input;
using GNOMES.Modules;
using GNOMES.Utilities;
using UnityEngine;

namespace Gnomes.Demo.Modules
{
    [Serializable]
    public class InteractModule : GnomesModule
    {
        // ── Intent fields (wizard-managed) ───────────────────────────────
        // WIZARD-BEGIN:InteractModule
        private readonly IntentTrigger _attack = new();
        public IntentTrigger Attack => _attack;
        private readonly IntentTrigger _interact = new();
        public IntentTrigger Interact => _interact;
        // WIZARD-END:InteractModule

        // ── State fields (add yours here) ────────────────────────────────
        // e.g. public ObservableValue<Vector3> Velocity = new();
        public ObservableValue<GnomesInteractable> CurrentHover = new();

        public override void ResetIntents()
        {
            // WIZARD-BEGIN-RESET:InteractModule
            _attack.ClearSubscribers();
            _interact.ClearSubscribers();
            // WIZARD-END-RESET:InteractModule
        }

        public override void Reset()
        {
            ResetIntents();
            // Clear your state ObservableValues here
            // e.g. Velocity.SetWithoutNotify(Vector3.zero);
            CurrentHover.Value = null;
        }
    }
}
