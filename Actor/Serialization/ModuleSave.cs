using System;

namespace GNOMES.Actor.Serialization {
    [Serializable]
    internal class ModuleSave
    {
        public string moduleTypeName;
        public string dataJson;     // ISaveable.Save() result serialized to JSON
    }
}