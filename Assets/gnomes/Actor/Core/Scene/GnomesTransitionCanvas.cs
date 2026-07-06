using System.Collections;
using UnityEngine;

namespace GNOMES.Actor.Core.Scene {
    /// <summary>
    /// Minimal DontDestroyOnLoad canvas that fades to a configurable color
    /// between scene transitions. Subscribes to GnomesSceneManager events
    /// automatically.
    ///
    /// Replace by building your own canvas and subscribing to the same events:
    ///   GnomesSceneManager.OnBeforeUnload  → fade in
    ///   GnomesSceneManager.OnSceneReady    → fade out
    ///   GnomesSceneManager.OnLoadProgress  → update loading bar
    ///
    /// This component will stay hidden if a custom canvas handles transitions.
    /// </summary>
    public class GnomesTransitionCanvas : MonoBehaviour
    {
        public static GnomesTransitionCanvas Instance { get; private set; }

        [Header("Appearance")]
        public Color OverlayColor = Color.black;

        [Header("Timing")]
        public float FadeInDuration  = 0.4f;
        public float FadeOutDuration = 0.4f;

        [Header("Loading Bar (optional)")]
        public UnityEngine.UI.Image LoadingBar;
        public GameObject           LoadingBarContainer;

        private UnityEngine.UI.Image    _overlay;
        [SerializeField]private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SetupOverlay();
            _canvasGroup.alpha          = 0f;
            _canvasGroup.blocksRaycasts = false;

            if (LoadingBarContainer != null) LoadingBarContainer.SetActive(false);

            GnomesSceneManager.OnLoadProgress += OnLoadProgress;
        }

        private void OnDestroy()
        {
            GnomesSceneManager.OnLoadProgress -= OnLoadProgress;
            if (Instance == this) Instance = null;
        }

        private void SetupOverlay()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas              = gameObject.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
            }

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _overlay = GetComponentInChildren<UnityEngine.UI.Image>();
            if (_overlay == null)
            {
                var overlayGo = new GameObject("Overlay");
                overlayGo.transform.SetParent(transform, false);
                _overlay = overlayGo.AddComponent<UnityEngine.UI.Image>();

                var rt = overlayGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            _overlay.color = OverlayColor;

            if (GetComponent<UnityEngine.UI.CanvasScaler>() == null)
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        internal IEnumerator FadeInRoutine()
        {
            _canvasGroup.blocksRaycasts = true;
            if (LoadingBarContainer != null)
            {
                LoadingBarContainer.SetActive(true);
                if (LoadingBar != null) LoadingBar.fillAmount = 0f;
            }
            yield return FadeRoutine(0f, 1f, FadeInDuration);
        }

        internal IEnumerator FadeOutRoutine()
        {
            if (LoadingBarContainer != null) LoadingBarContainer.SetActive(false);
            yield return FadeRoutine(1f, 0f, FadeOutDuration);
            _canvasGroup.blocksRaycasts = false;
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed           += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        private void OnLoadProgress(float progress)
        {
            if (LoadingBar != null) LoadingBar.fillAmount = progress;
        }
    }
}