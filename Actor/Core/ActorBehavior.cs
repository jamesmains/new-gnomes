using System;
using GNOMES.Modules;

namespace GNOMES.Actor.Core {
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public abstract class ActorBehavior
    {
        protected Actor Parent  { get; private set; }
        protected bool  IsValid { get; private set; }

        public virtual void Bind(Actor parent)
        {
            if (Parent != null && Parent != parent)
                Unbind();

            Parent  = parent;
            IsValid = VerifyRequirements();
            if (IsValid) OnPostBind();
        }

        protected abstract bool VerifyRequirements();
        protected virtual  void OnPostBind() { }

        public virtual void Unbind()
        {
            Parent  = null;
            IsValid = false;
        }

        public virtual void Dispose()      { }
        public virtual void Update()       { }
        public virtual void FixedUpdate()  { }
        public virtual void LateUpdate()   { }

        /// <summary>Null-safe module fetch — returns null before Bind or after Unbind.</summary>
        protected T GetModule<T>() where T : class, IBrainModule =>
            Parent?.Brain?.GetModule<T>();

        public virtual void SendData<T>(T data) { }
    }
}