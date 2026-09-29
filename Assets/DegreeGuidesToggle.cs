using UnityEngine;
using UnityEngine.UI;

/// <summary>Wrist Settings → Guidance boundary: show/hide all wall outlines and degree labels.</summary>
[RequireComponent(typeof(Toggle))]
public class DegreeGuidesToggle : MonoBehaviour
{
    Toggle m_Toggle;
    FindObjectGameManager m_Game;

    void OnEnable() => BindAndSync();
    void Start() => BindAndSync();

    // Resolve again at Start if the menu enabled before the game manager existed.
    // Reopening the wrist menu reflects changes made through the public game API.
    void BindAndSync()
    {
        m_Toggle = GetComponent<Toggle>();
        RayMenuFeedback.Attach(m_Toggle);
        if (m_Game == null)
        {
            m_Game = FindFirstObjectByType<FindObjectGameManager>();
            if (m_Game != null)
            {
                m_Toggle.onValueChanged.AddListener(m_Game.SetDegreeGuidesVisible);
                m_Game.DegreeGuidesVisibilityChanged += SyncToggle;
            }
        }
        m_Toggle.interactable = m_Game != null;
        if (m_Game != null) SyncToggle(m_Game.DegreeGuidesVisible);
    }

    void SyncToggle(bool visible) => m_Toggle.SetIsOnWithoutNotify(visible);

    void OnDestroy()
    {
        if (m_Toggle != null && m_Game != null)
        {
            m_Toggle.onValueChanged.RemoveListener(m_Game.SetDegreeGuidesVisible);
            m_Game.DegreeGuidesVisibilityChanged -= SyncToggle;
        }
    }
}
