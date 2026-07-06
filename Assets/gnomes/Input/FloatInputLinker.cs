using GNOMES.Modules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GNOMES.Input {
    /// <summary>
    /// Binds a single-axis float action to an IntentValue&lt;float&gt; field
    /// on a module (Zoom, Throttle, Lean etc.).
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Float Linker",
        menuName  = "Gnomes/Input/Float Linker")]
    public class FloatInputLinker : InputLinker
    {
        private IntentValue<float> _intent;

        public override void Bind(PlayerInput input, IBrainModule module)
        {
            _action = ResolveAction(input);
            if (_action == null) return;

            _intent = ResolveIntentValue<float>(module, IntentFieldName);
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
            _intent.Value = ctx.ReadValue<float>();

        private void OnCanceled(InputAction.CallbackContext ctx) =>
            _intent.Value = 0f;
    }
}