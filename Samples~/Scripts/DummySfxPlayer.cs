using System.Collections.Generic;
using GNOMES.Audio;
using GNOMES.Utilities;
using TMPro;
using Button = UnityEngine.UI.Button;

namespace UnityEngine {
    public class DummySfxPlayer: MonoBehaviour {

        [Header("Dependencies")]
        [SerializeField] private SFXLibrary Library;
        [SerializeField] private RectTransform SfxCategoryListContainer;
        [SerializeField] private RectTransform SfxItemListContainer;
        [SerializeField] private RectTransform CategoryPointerGfx;
        [SerializeField] private RectTransform ListPointerGfx;

        [Header("Prefabs")] 
        [SerializeField] private GameObject SfxListItemPrefab;

        [Header("Cache")] 
        [SerializeField] private string SelectedCategory;
        [SerializeField] private int SelectedCategoryId;
        [SerializeField] private int SelectedSfxId;
        [SerializeField] private List<GameObject> SpawnedSfxItems = new();

        private void Awake() {
            BuildCategoryList();
            BuildSfxList(Library.Entries[SelectedCategoryId]);
        }

        private void BuildCategoryList() {
            SelectedCategory = Library.Entries[0].Key;
            for (var index = 0; index < Library.Entries.Count; index++) {
                var item = Library.Entries[index];
                int i = index;
                // Create button
                var obj = Pooler.Spawn(SfxListItemPrefab, SfxCategoryListContainer, false).GetComponent<RectTransform>();
                if (obj.TryGetComponent(out Button button)) {
                    button.onClick.AddListener(() => {
                        SelectedCategory = item.Key;
                        SelectedCategoryId = i;
                        SelectedSfxId = -1;
                        BuildSfxList(item);
                        CategoryPointerGfx.position = obj.position - new Vector3(obj.sizeDelta.x + 35f, 0, 0);
                    });
                    if (index == 0) {
                        CategoryPointerGfx.position = obj.position - new Vector3(obj.sizeDelta.x + 35f, 0, 0);
                    }
                    if (button.transform.GetChild(0).TryGetComponent(out TextMeshProUGUI text)) {
                        text.text = item.Key;
                    }
                }
                else {
                    Debug.LogWarning($"[SFX Demo] No button found on {obj.name} prefab");
                    obj.gameObject.SetActive(false);
                }
            }
        }

        private void BuildSfxList(SFXLibrary.Entry libraryEntry) {
            foreach (var o in SpawnedSfxItems) {
                o.SetActive(false);
            }
            SpawnedSfxItems.Clear();

            for (var index = 0; index < libraryEntry.Clips.Length; index++) {
                var item = libraryEntry.Clips[index];
                int i = index;

                // Create button
                var obj = Pooler.Spawn(SfxListItemPrefab, SfxItemListContainer, false).GetComponent<RectTransform>();
                if (obj.TryGetComponent(out Button button)) {
                    button.onClick.AddListener(() => {
                        ListPointerGfx.position = obj.position + new Vector3(obj.sizeDelta.x + 35f, 0, 0);
                        SelectedSfxId = i;
                    });
                    if (index == 0) {
                        ListPointerGfx.position = obj.position - new Vector3(obj.sizeDelta.x + 35f, 0, 0);
                    }
                    if (button.transform.GetChild(0).TryGetComponent(out TextMeshProUGUI text)) {
                        text.text = item.name;
                    }
                    SpawnedSfxItems.Add(obj.gameObject);
                }
                else {
                    Debug.LogWarning($"[SFX Demo] No button found on {obj.name} prefab");
                    obj.gameObject.SetActive(false);
                }
            }
        }

        public void PlayClip() {
            if (SelectedSfxId == -1) return;
            Debug.Log(SelectedSfxId);
            GnomesSFXManager.Play(Library.Entries[SelectedCategoryId].Clips[SelectedSfxId]);
        }

        public void PlayRandomClip() {
            if (string.IsNullOrEmpty(SelectedCategory)) return;
            GnomesSFXManager.Play(Library, SelectedCategory);
        }
    }
}