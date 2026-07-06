using System;
using System.Collections.Generic;

namespace GNOMES.Actor.Serialization {
    [Serializable]
    internal class ActorSave
    {
        public string             guid;
        public string             prefabPath;     // null for scene actors
        public float[]            position;       // Vector3 as float[3]
        public float[]            rotation;       // Quaternion as float[4]
        public List<ModuleSave>   modules = new();
    }
}