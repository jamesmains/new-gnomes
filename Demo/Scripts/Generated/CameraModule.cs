// Created by Gnomes Setup Wizard
// Add your state fields below the WIZARD-END marker.
// Do not edit between the WIZARD markers manually.

using System;
using GNOMES.Input;
using GNOMES.Modules;
using UnityEngine;

namespace Gnomes.Demo.Modules
{
    [Serializable]
    public class CameraModule : GnomesModule
    {
        // ── Intent fields (wizard-managed) ───────────────────────────────
        // WIZARD-BEGIN:CameraModule
        private readonly IntentValue<UnityEngine.Vector2> _look = new();
        public IntentValue<UnityEngine.Vector2> Look => _look;
        // WIZARD-END:CameraModule

        // ── State fields (add yours here) ────────────────────────────────
        // e.g. public ObservableValue<Vector3> Velocity = new();
        public Camera Camera;

        public override void ResetIntents()
        {
            // WIZARD-BEGIN-RESET:CameraModule
            _look.Value = default;
            // WIZARD-END-RESET:CameraModule
        }

        public override void Reset()
        {
            ResetIntents();
            // Clear your state ObservableValues here
            // e.g. Velocity.SetWithoutNotify(Vector3.zero);
        }
    }
}
