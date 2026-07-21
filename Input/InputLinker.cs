// ═════════════════════════════════════════════════════════════════════════════
//  InputLinkers.cs — Parent House Framework · Gnomes
//  Contains: InputLinker (base), Vector2InputLinker,
//            FloatInputLinker, ButtonInputLinker
//
//  These are the three shapes that cover every possible Unity input action
//  type. The setup wizard creates instances of these as .asset files —
//  you should never need to subclass them.
// ═════════════════════════════════════════════════════════════════════════════

using System.Reflection;
using GNOMES.Modules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GNOMES.Input
{
    
    /// <summary>
    /// Base class for all input linkers. One linker asset = one input action
    /// bound to one intent field on a module. Created and wired automatically
    /// by the Gnomes Setup Wizard — do not subclass manually.
    /// </summary>
    
    public abstract class InputLinker : ScriptableObject
    {
        [Tooltip("Must match the action name in your .inputActions asset exactly.")]
        public string ActionName;

        [Tooltip("The intent field name on the target module. Set by the Setup Wizard.")]
        public string IntentFieldName;

        /// <summary>
        /// The fully-qualified type name of the module this linker targets.
        /// Set by the Setup Wizard. Used by PlayerBrain.BindPlayer to route
        /// each linker to the correct module on the brain.
        /// </summary>
        public string TargetModuleTypeName;

        protected InputAction _action;

        /// <summary>
        /// Resolves the action from <paramref name="input"/> and subscribes
        /// to callbacks, writing to the matching intent field on
        /// <paramref name="module"/>. Called by PlayerBrain.BindPlayer.
        /// </summary>
        public abstract void Bind(PlayerInput input, IBrainModule module);

        /// <summary>Unsubscribes all callbacks and disables the action.</summary>
        public virtual void Unbind()
        {
            if (_action == null) return;
            _action.Disable();
            _action = null;
        }

        // ── Shared resolution helpers ─────────────────────────────────────────
        // Paid once at bind time via reflection — never per frame.

        protected InputAction ResolveAction(PlayerInput input)
        {
            if (input == null)
            {
                Debug.LogError($"[Gnomes] {GetType().Name} '{name}': PlayerInput is null.");
                return null;
            }

            var action = input.actions[ActionName];
            if (action == null)
                Debug.LogError(
                    $"[Gnomes] {GetType().Name} '{name}': no action named " +
                    $"'{ActionName}' in '{input.actions.name}'.");

            return action;
        }

        /// <summary>
        /// Resolves an IntentValue&lt;T&gt; property by name from the module.
        /// Walks the full type hierarchy. Paid once at bind time.
        /// </summary>
        protected static IntentValue<T> ResolveIntentValue<T>(
            IBrainModule module, string fieldName)
        {
            if (module == null)
            {
                Debug.LogError("[Gnomes] ResolveIntentValue: module is null.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                Debug.LogError("[Gnomes] ResolveIntentValue: field name is empty.");
                return null;
            }

            for (var t = module.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty(fieldName,
                    BindingFlags.Instance | BindingFlags.Public);

                if (prop != null &&
                    typeof(IntentValue<T>).IsAssignableFrom(prop.PropertyType))
                {
                    var value = prop.GetValue(module) as IntentValue<T>;
                    if (value != null) return value;
                }
            }

            Debug.LogError(
                $"[Gnomes] ResolveIntentValue: no IntentValue<{typeof(T).Name}> " +
                $"property named '{fieldName}' on '{module.GetType().Name}'. " +
                $"Regenerate via Tools/Gnomes/Project Setup.");

            return null;
        }

        /// <summary>
        /// Resolves an IntentTrigger property by name from the module.
        /// Paid once at bind time.
        /// </summary>
        protected static IntentTrigger ResolveIntentTrigger(
            IBrainModule module, string fieldName)
        {
            if (module == null)
            {
                Debug.LogError("[Gnomes] ResolveIntentTrigger: module is null.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                Debug.LogError("[Gnomes] ResolveIntentTrigger: field name is empty.");
                return null;
            }

            for (var t = module.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty(fieldName,
                    BindingFlags.Instance | BindingFlags.Public);

                if (prop != null &&
                    typeof(IntentTrigger).IsAssignableFrom(prop.PropertyType))
                {
                    var value = prop.GetValue(module) as IntentTrigger;
                    if (value != null) return value;
                }
            }

            Debug.LogError(
                $"[Gnomes] ResolveIntentTrigger: no IntentTrigger property " +
                $"named '{fieldName}' on '{module.GetType().Name}'. " +
                $"Regenerate via Tools/Gnomes/Project Setup.");

            return null;
        }
    }
}