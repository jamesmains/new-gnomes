using UnityEngine;
using UnityEngine.UI;

namespace GNOMES.Actor.Core.Interaction {
    /// <summary>
    /// A single row in the interaction prompt. Populate icon, keybind hint,
    /// and label from an InteractionOption.
    ///
    /// Create a prefab with this component and assign UI Image/Text children
    /// in the inspector. The prompt instantiates one row per enabled option.
    /// </summary>
    public class GnomesInteractionPromptRow : MonoBehaviour
    {
        [Tooltip("Displays the option's Icon sprite. Can be null if unused.")]
        public Image  IconImage;

        [Tooltip("Displays the keybind hint, e.g. 'E' or 'F'.")]
        public TMPro.TextMeshProUGUI KeybindText;

        [Tooltip("Displays the option label, e.g. 'Pick Up' or 'Examine'.")]
        public TMPro.TextMeshProUGUI LabelText;

        /// <summary>Populates this row from an InteractionOption.</summary>
        public void Populate(InteractionOption option)
        {
            if (IconImage != null)
            {
                IconImage.sprite  = option.Icon;
                IconImage.enabled = option.Icon != null;
            }

            if (KeybindText != null)
                KeybindText.text = option.KeybindHint ?? "";

            if (LabelText != null)
                LabelText.text = option.Label ?? "";
        }
    }
}