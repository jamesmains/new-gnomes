namespace GNOMES.Input {
    /// <summary>
    /// A typed intent value. Written by brains (or input linkers);
    /// read by behaviours. Intentionally simpler than ObservableValue —
    /// no change notification, no locking, zero overhead per frame.
    /// </summary>
    public class IntentValue<T>
    {
        private T _value;

        public T Value
        {
            get => _value;
            set => _value = value;
        }
    }
}