using GNOMES.Modules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GNOMES.Input {
    /// <summary>
    /// Binds a button action to an IntentTrigger field on a module
    /// (Jump, Roll, Interact, Handbrake etc.).
    ///
    /// For held button state (IsSprinting, IsCrouching) use FloatInputLinker
    /// with a value binding instead — triggers are fire-and-forget.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Button Linker",
        menuName  = "Gnomes/Input/Button Linker")]
    public class ButtonInputLinker : InputLinker
    {
        private IntentTrigger _intent;

        public override void Bind(PlayerInput input, IBrainModule module)
        {
            _action = ResolveAction(input);
            if (_action == null) return;

            _intent = ResolveIntentTrigger(module, IntentFieldName);
            if (_intent == null) return;

            _action.performed += OnPerformed;
            _action.Enable();
        }

        public override void Unbind()
        {
            if (_action != null)
                _action.performed -= OnPerformed;

            _intent = null;
            base.Unbind();
        }

        private void OnPerformed(InputAction.CallbackContext ctx) =>
            _intent.Invoke();
    }
}