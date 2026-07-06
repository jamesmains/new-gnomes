using System;
using System.Collections.Generic;
using UnityEngine;

// [Serializable]
// public class ItemDictionary : UnitySerializedDictionary<Item, int>{}

namespace GNOMES.Utilities {
    public abstract class UnitySerializedDictionary<TKey, TValue> :
        Dictionary<TKey, TValue>, ISerializationCallbackReceiver {
        private readonly List<KeyValueData> keyValueData = new();

        public void OnAfterDeserialize() {
            Clear();
            foreach (var item in keyValueData) this[item.key] = item.value;
        }

        public void OnBeforeSerialize() {
            keyValueData.Clear();
            foreach (var kvp in this)
                keyValueData.Add(new KeyValueData {
                    key = kvp.Key,
                    value = kvp.Value
                });
        }

        [Serializable]
        private struct KeyValueData {
            public TKey key;
            public TValue value;
        }
    }
}