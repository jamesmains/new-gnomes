using GNOMES.Modules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GNOMES.Input {
    /// <summary>
    /// Binds a Vector2 input action to an IntentValue&lt;Vector2&gt; field
    /// on a module (Move, Look, Aim, Steer etc.).
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Vector2 Linker",
        menuName  = "Gnomes/Input/Vector2 Linker")]
    public class Vector2InputLinker : InputLinker
    {
        private IntentValue<Vector2> _intent;

        public override void Bind(PlayerInput input, IBrainModule module)
        {
            _action = ResolveAction(input);
            if (_action == null) return;

            _intent = ResolveIntentValue<Vector2>(module, IntentFieldName);
            if (_intent == null) return;

            _action.performed += OnPerformed;
            _action.canceled  += OnCanceled;
            _action.Enable();
        }

        public override void Unbind()
        {
            if (_action != null)
            {
                _action.performed -= OnPerformed;
                _action.canceled  -= OnCanceled;
            }
            _intent = null;
            base.Unbind();
        }

        private void OnPerformed(InputAction.CallbackContext ctx) =>
            _intent.Value = ctx.ReadValue<Vector2>();

        private void OnCanceled(InputAction.CallbackContext ctx) =>
            _intent.Value = Vector2.zero;
    }
}