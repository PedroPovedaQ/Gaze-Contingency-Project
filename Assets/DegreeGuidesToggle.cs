using UnityEngine;
using UnityEngine.UI;

/// <summary>Controls the surrounding degree labels and outline lines from wrist settings.</summary>
[RequireComponent(typeof(Toggle))]
public class DegreeGuidesToggle : MonoBehaviour
{
    Toggle m_Toggle;
    FindObjectGameManager m_Game;

    void Start()
    {
        m_Game = FindFirstObjectByType<FindObjectGameManager>();
        m_Toggle = GetComponent<Toggle>();
        if (m_Game == null) { m_Toggle.interactable = false; return; }
        m_Toggle.SetIsOnWithoutNotify(m_Game.DegreeGuidesVisible);
        m_Toggle.onValueChanged.AddListener(m_Game.SetDegreeGuidesVisible);
    }

    void OnDestroy()
    {
        if (m_Toggle != null && m_Game != null)
            m_Toggle.onValueChanged.RemoveListener(m_Game.SetDegreeGuidesVisible);
    }
}
