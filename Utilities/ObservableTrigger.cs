using System;

namespace GNOMES.Utilities {
    /// <summary>
    /// A fire-and-forget event channel for state changes that don't need
    /// a value (grounded, landed, died etc.).
    /// </summary>
    public class ObservableTrigger
    {
        public event Action OnTriggered;
        public void Invoke()           => OnTriggered?.Invoke();
        public void ClearSubscribers() => OnTriggered = null;
    }
}