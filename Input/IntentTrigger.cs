using System;
using GNOMES.Modules;

namespace GNOMES.Input {
    /// <summary>
    /// A fire-once intent trigger. Brains invoke; behaviours subscribe.
    /// </summary>
    public class IntentTrigger
    {
        public event Action OnTriggered;
        public void Invoke()           => OnTriggered?.Invoke();
        public void ClearSubscribers() => OnTriggered = null;
        
        // ── Intent resolution helpers ─────────────────────────────────────────
        // Resolves intent fields from a module by property name via reflection.
        // Paid once in OnPostBind — never per frame.
 
        public static IntentValue<T> ResolveIntent<T>(
            IBrainModule module, string fieldName)
        {
            if (module == null || string.IsNullOrEmpty(fieldName)) return null;
 
            for (var t = module.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);
 
                if (prop != null &&
                    typeof(IntentValue<T>).IsAssignableFrom(prop.PropertyType))
                    return prop.GetValue(module) as IntentValue<T>;
            }
 
            // Not finding the field is expected on a fresh project before the
            // wizard has been run — don't error, just degrade to zero input.
            return null;
        }
 
        public static IntentTrigger ResolveIntentTrigger(
            IBrainModule module, string fieldName)
        {
            if (module == null || string.IsNullOrEmpty(fieldName)) return null;
 
            for (var t = module.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);
 
                if (prop != null &&
                    typeof(IntentTrigger).IsAssignableFrom(prop.PropertyType))
                    return prop.GetValue(module) as IntentTrigger;
            }
 
            return null;
        }
    }
    
}