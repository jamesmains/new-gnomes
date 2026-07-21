using System;
using System.Collections.Generic;

namespace GNOMES.Utilities {
    /// <summary>
    /// A typed state value with change notification.
    /// Written by behaviours; read by other behaviours and systems.
    /// Locking prevents writes — reads always return the last committed value.
    /// </summary>
    public class ObservableValue<T>
    {
        private T   _value;
        private int _lockCount;

        public Action<T> OnChanged;

        public T Value
        {
            get => _value;
            set
            {
                if (_lockCount > 0) return;
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                OnChanged?.Invoke(_value);
            }
        }

        public void SetWithoutNotify(T value) => _value = value;
        public void ForceInvoke()             => OnChanged?.Invoke(_value);

        public void IncrementLock()     => _lockCount++;
        public void DecrementLock()     => _lockCount = Math.Max(0, _lockCount - 1);
        public bool IsLocked            => _lockCount > 0;
        public void SetLockValue(int v) => _lockCount = Math.Max(0, v);
    }
}