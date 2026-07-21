using System;
using GNOMES.Modules;

namespace GNOMES.Runtime {
    /// <summary>
    /// Declares that this ActorBehavior requires a specific module to be present
    /// on the brain before it will function. Apply multiple times for multiple
    /// requirements. Validated at edit time by GnomesValidator.
    ///
    /// Usage:
    ///   [RequiresModule(typeof(LocomotionModule))]
    ///   public class Movement3dBehavior : ActorBehavior { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class RequiresModuleAttribute : Attribute
    {
        public Type ModuleType { get; }
 
        public RequiresModuleAttribute(Type moduleType)
        {
            if (!typeof(IBrainModule).IsAssignableFrom(moduleType))
                throw new ArgumentException(
                    $"{moduleType.Name} does not implement IBrainModule.",
                    nameof(moduleType));
 
            ModuleType = moduleType;
        }
    }
}