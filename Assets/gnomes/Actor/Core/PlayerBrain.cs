using System;
using System.Collections.Generic;
using System.Linq;
using GNOMES.Input;
using GNOMES.Modules;
using GNOMES.Runtime;
using UnityEngine;

namespace GNOMES.Actor.Core {
    [Serializable]
    [CreateAssetMenu(fileName = "New Player Brain", menuName = "Gnomes/Brains/Player Brain")]
    public class PlayerBrain : ActorBrain
    {
        [HideInInspector] public Player Player;

        [SerializeReference, Gnomeable]
        public CameraBehavior DefaultCameraBehavior;
        public List<InputLinker> Linkers;
 
        public override void Bind(GNOMES.Actor.Core.Actor actor)
        {
            base.Bind(actor);
 
            // DemoGameController sets Player before calling SwapBrain, so
            // prefer the pre-set reference. Fall back to GetComponent for
            // cases where the actor itself carries a Player component.
            var player = Player ?? actor.GetComponent<Player>();
            if (player != null)
                BindPlayer(player);
        }
 
        public override void Unbind()
        {
            UnbindPlayer();
            base.Unbind();
        }
 
        // Runtime linker instances — cloned from the asset-level Linkers list
        // on each BindPlayer call so multiple players never share the same
        // InputLinker instance (and therefore never share _action / _intent state).
        private List<InputLinker> _runtimeLinkers;
 
        /// <summary>
        /// Connects this brain to a Player and wires all InputLinkers.
        /// Linker assets are cloned at bind time so multiple simultaneous
        /// players each get independent callback subscriptions.
        /// </summary>
        public virtual void BindPlayer(Player boundPlayer)
        {
            if (boundPlayer == null)
            {
                Debug.LogWarning("[Gnomes] PlayerBrain.BindPlayer called with null Player.");
                return;
            }
 
            UnbindPlayer();
            Player = boundPlayer;
 
            // Clone every linker asset into a fresh in-memory instance.
            // The asset is the template; _runtimeLinkers are the live copies.
            // ScriptableObject.Instantiate produces a full independent copy
            // with its own _action and _intent fields.
            _runtimeLinkers = Linkers
                .Where(l => l != null)
                .Select(l => Instantiate(l))
                .ToList();
 
            foreach (var linker in _runtimeLinkers)
            {
                var targetModule = ResolveLinkerModule(linker);
                if (targetModule == null)
                {
                    Debug.LogWarning(
                        $"[Gnomes] PlayerBrain '{name}': could not resolve " +
                        $"target module for linker '{linker.name}'. " +
                        $"Regenerate via Tools/Gnomes/Project Setup.");
                    continue;
                }
 
                linker.Bind(Player.PlayerInput, targetModule);
            }
        }
 
        /// <summary>
        /// Disconnects all runtime linkers and destroys the clones.
        /// The original Linkers asset list is never touched.
        /// </summary>
        public virtual void UnbindPlayer()
        {
            if (_runtimeLinkers != null)
            {
                foreach (var linker in _runtimeLinkers)
                {
                    if (linker == null) continue;
                    linker.Unbind();
                    Destroy(linker);   // clean up the clone — don't leak ScriptableObjects
                }
                _runtimeLinkers = null;
            }
 
            //Player = null;
        }
 
        /// <summary>
        /// Finds the module on this brain that matches the linker's
        /// TargetModuleTypeName. Resolved once per linker per bind — not per frame.
        /// </summary>
        private IBrainModule ResolveLinkerModule(InputLinker linker)
        {
            // TargetModuleTypeName is set by the Setup Wizard on the linker asset
            string typeName = linker.TargetModuleTypeName;
            if (string.IsNullOrEmpty(typeName)) return null;
 
            foreach (var module in Modules)
            {
                if (module == null) continue;
                if (module.GetType().Name == typeName ||
                    module.GetType().FullName == typeName)
                    return module;
            }
 
            return null;
        }
    }
}