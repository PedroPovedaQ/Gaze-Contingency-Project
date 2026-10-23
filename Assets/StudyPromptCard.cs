using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared presentation for study instructions. The prefab retains the rounded
/// surface and Inter typography of the original “Tap a surface” coaching card.
/// Layout and actions remain owned by each screen, so styling cannot advance a trial.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class StudyPromptCard : MonoBehaviour
{
    [SerializeField] TMP_FontAsset m_Font;
    [SerializeField] Color m_TextColor = new Color(.89411765f, .89411765f, .89411765f, 1);
    static StudyPromptCard s_Template;
    static StudyPromptCard Template => s_Template != null ? s_Template :
        s_Template = Resources.Load<StudyPromptCard>("UI/StudyPromptCard");

    public static GameObject Create(Transform parent, string name, Vector2 size)
    {
        var card = Instantiate(Template, parent, false);
        card.name = name;
        card.GetComponent<RectTransform>().sizeDelta = size;
        return card.gameObject;
    }

    public static void StyleText(TMP_Text text, float maximumSize)
    {
        text.font = Template.m_Font;
        text.color = Template.m_TextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = maximumSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(18f, maximumSize);
        text.fontSizeMax = maximumSize;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    public static void StyleButton(Image image)
    {
        image.sprite = Template.GetComponent<Image>().sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(.22f, .22f, .22f, 1);
        // Preserve controller hit testing on both buttons and card backgrounds.
        image.raycastTarget = true;
    }
}
