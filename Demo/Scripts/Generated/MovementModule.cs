// Created by Gnomes Setup Wizard
// Add your state fields below the WIZARD-END marker.
// Do not edit between the WIZARD markers manually.

using System;
using GNOMES.Input;
using GNOMES.Modules;
using GNOMES.Utilities;
using UnityEngine;

namespace Gnomes.Demo.Modules
{
    [Serializable]
    public class MovementModule : GnomesModule
    {
        // ── Intent fields (wizard-managed) ───────────────────────────────
        // WIZARD-BEGIN:MovementModule
        private readonly IntentValue<UnityEngine.Vector2> _move = new();
        public IntentValue<UnityEngine.Vector2> Move => _move;
        private readonly IntentTrigger _crouch = new();
        public IntentTrigger Crouch => _crouch;
        private readonly IntentTrigger _jump = new();
        public IntentTrigger Jump => _jump;
        // WIZARD-END:MovementModule

        // ── State fields (add yours here) ────────────────────────────────
        // e.g. public ObservableValue<Vector3> Velocity = new();
        public ObservableValue<Vector3> Velocity = new();
        public ObservableValue<Vector3> FacingDirection = new();
        public ObservableValue<Vector3> CurrentFacingDirection = new();
        public ObservableValue<bool> IsGrounded = new();

        public override void ResetIntents()
        {
            // WIZARD-BEGIN-RESET:MovementModule
            _move.Value = default;
            _crouch.ClearSubscribers();
            _jump.ClearSubscribers();
            // WIZARD-END-RESET:MovementModule
        }

        public override void Reset()
        {
            ResetIntents();
            // Clear your state ObservableValues here
            // e.g. Velocity.SetWithoutNotify(Vector3.zero);
            Velocity.SetWithoutNotify(Vector3.zero);
            FacingDirection.SetWithoutNotify(Vector3.zero);
            CurrentFacingDirection.SetWithoutNotify(Vector3.zero);
            IsGrounded.SetWithoutNotify(false);
        }
    }
}
