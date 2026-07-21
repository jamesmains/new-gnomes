using System.Collections.Generic;
using Gnomes.Demo.Modules;
using GNOMES.Utilities;
using UnityEngine;

namespace GNOMES.Actor.Core.Interaction {
    /// <summary>
    /// UI component that listens to an actor's InteractionModule.CurrentHover
    /// and renders the available interaction options as icon + keybind + label
    /// rows. Place on a Canvas GameObject and assign the actor to watch.
    ///
    /// Layout per option:
    ///   [ Icon ] [ KeybindHint ] [ Label ]
    ///
    /// The prompt hides automatically when nothing is hovered.
    ///
    /// For multiple actors (split-screen) create one prompt per player and
    /// assign the appropriate actor to each.
    /// </summary>
    public class GnomesInteractionPrompt : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Target")]
        [Tooltip("The actor whose InteractionModule this prompt listens to.")]
        public Actor TargetActor;

        [Header("Prefabs")]
        [Tooltip("Prefab for a single option row. " +
                 "Must have GnomesInteractionPromptRow on the root.")]
        public GnomesInteractionPromptRow RowPrefab;

        [Tooltip("Parent transform where rows are instantiated. " +
                 "Use a VerticalLayoutGroup for automatic stacking.")]
        public RectTransform RowContainer;

        [Header("Animation")]
        [Tooltip("How fast the prompt fades in and out.")]
        public float FadeSpeed = 8f;

        public bool ShowInWorldSpace = false;

        // ── Runtime ───────────────────────────────────────────────────────────

        private InteractModule        _interactModule;
        private CameraModule          _cameraModule;
        private CanvasGroup              _canvasGroup;
        private readonly List<GnomesInteractionPromptRow> _rows = new();
        private float                    _targetAlpha;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
            _targetAlpha       = 0f;
        }

        private void Start()
        {
            if (TargetActor != null)
                Bind(TargetActor);
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (_interactModule?.CurrentHover != null && ShowInWorldSpace
                && _cameraModule != null && _cameraModule.Camera != null) {
                var worldPos =
                    _cameraModule.Camera.WorldToScreenPoint(_interactModule.CurrentHover.Value.transform.position);
                RowContainer.position = worldPos;
                Debug.Log(worldPos);
            }
            
            // Smooth fade in/out
            _canvasGroup.alpha = Mathf.Lerp(
                _canvasGroup.alpha, _targetAlpha,
                Time.deltaTime * FadeSpeed);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Binds the prompt to an actor. Call this when the player is assigned
        /// to an actor at runtime (e.g. after SpawnPlayer).
        /// </summary>
        public void Bind(Actor actor)
        {
            Unbind();
            TargetActor = actor;

            _interactModule = actor?.Brain?.GetModule<InteractModule>();
            if (_interactModule == null) return;

            _cameraModule = actor?.Brain?.GetModule<CameraModule>();

            _interactModule.CurrentHover.OnChanged += OnHoverChanged;

            // Reflect current state in case something is already hovered
            OnHoverChanged(_interactModule.CurrentHover.Value);
        }

        /// <summary>Detaches from the current actor.</summary>
        public void Unbind()
        {
            if (_interactModule != null)
                _interactModule.CurrentHover.OnChanged -= OnHoverChanged;

            _interactModule = null;
            ClearRows();
            _targetAlpha = 0f;
        }

        // ── Hover change handler ──────────────────────────────────────────────

        private void OnHoverChanged(GnomesInteractable interactable)
        {
            ClearRows();

            if (interactable == null)
            {
                _targetAlpha = 0f;
                return;
            }

            var options = interactable.GetEnabledOptions();
            if (options.Count == 0)
            {
                _targetAlpha = 0f;
                return;
            }

            foreach (var option in options)
            {
                if (RowPrefab == null || RowContainer == null) break;

                var row = Pooler.Spawn(RowPrefab.gameObject, RowContainer,false).GetComponent<GnomesInteractionPromptRow>();
                row.Populate(option);
                _rows.Add(row);
            }

            _targetAlpha = 1f;
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
                if (row != null) row.gameObject.SetActive(false);
            _rows.Clear();
        }
    }
}