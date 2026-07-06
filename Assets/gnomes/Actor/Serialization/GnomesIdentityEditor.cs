#if UNITY_EDITOR
using UnityEditor;

namespace GNOMES.Actor.Serialization {
    /// <summary>
    /// Ensures GnomesIdentity always has a GUID assigned in the editor.
    /// Shows the GUID as read-only for debugging and copy-paste.
    /// </summary>
    [CustomEditor(typeof(GnomesIdentity))]
    internal class GnomesIdentityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var identity = (GnomesIdentity)target;

            // Ensure GUID exists — handles the case where the component
            // was added before this editor existed
            if (string.IsNullOrEmpty(identity.Guid))
            {
                identity.AssignGuidIfEmpty();
                EditorUtility.SetDirty(identity);
            }

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("GUID", identity.Guid);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "This GUID uniquely identifies this actor in save data. " +
                "Do not modify it manually — use the right-click context menu " +
                "to regenerate if needed.",
                MessageType.None);

            if (string.IsNullOrEmpty(identity.Guid))
                EditorGUILayout.HelpBox(
                    "No GUID assigned. This actor will not be saved correctly.",
                    MessageType.Error);
        }
    }
}
#endif