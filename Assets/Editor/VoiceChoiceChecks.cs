using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VoiceChoiceChecks
{
    [Serializable] sealed class CatalogSnapshot { public int schema; public string catalog_version, provider; public CatalogCandidate[] candidates; }
    [Serializable] sealed class CatalogCandidate { public string gender_presentation, name, request_voice_id; public int option; }
    static FieldInfo Field(string name) => typeof(VoiceModeSelector).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    static string Phase(VoiceModeSelector selector) => Field("m_Phase").GetValue(selector).ToString();
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    [MenuItem("Tools/Codex/Check Voice Choices")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before checks.");
        var gender = SessionConfig.NeutralProfile;
        int option = SessionConfig.NeutralVoiceOption;
        var voice = SessionConfig.Voice;
        var host = new GameObject("VoiceChoiceFixture"); host.SetActive(false);
        try
        {
            var serialized = new System.IO.StringWriter();
            SessionConfig.WriteNeutralCatalog(serialized);
            var catalog = JsonUtility.FromJson<CatalogSnapshot>(serialized.ToString());
            Check(catalog.schema == 1 && catalog.catalog_version == SessionConfig.NeutralCatalogVersion &&
                catalog.provider == "elevenlabs" && catalog.candidates.Length == 12,"Catalog JSON contains all twelve versioned identities");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var candidate in catalog.candidates)
            {
                var candidateGender = (NeutralVoiceProfile)Enum.Parse(typeof(NeutralVoiceProfile), candidate.gender_presentation);
                Check(candidate.name == SessionConfig.NeutralName(candidateGender,candidate.option) &&
                    candidate.request_voice_id == SessionConfig.NeutralId(candidateGender,candidate.option) && ids.Add(candidate.request_voice_id),
                    "Catalog inventory matches each unique stable option");
            }
            var selector = host.AddComponent<VoiceModeSelector>();
            selector.Initialize(null);
            typeof(VoiceModeSelector).GetMethod("RefreshChoices", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(selector, null);
            var canvas = (GameObject)Field("m_CanvasGO").GetValue(selector);
            Check(Array.Exists(canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(),
                label => label.text == "Quick test — neutral voice"), "Start panel exposes the neutral quick-test ray button.");
            Check(Array.Exists(canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(),
                label => label.text == "Quick test — last cloned voice"), "Start panel exposes explicit last-clone shortcut.");
            Check(Array.Exists(canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(),
                label => label.text == "Quick test — new cloned voice"), "Start panel exposes fresh recording shortcut.");
            selector.StartNewCloneQuickTest();
            Check(Phase(selector) == "WaitingToStart" && !selector.IsComplete,
                "Fresh-clone shortcut also requires arming and an audio system.");
            var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>();
            foreach (var button in buttons)
            {
                var rect = button.GetComponent<RectTransform>();
                Check(rect.anchoredPosition.y - rect.sizeDelta.y / 2 >= -240 &&
                    rect.anchoredPosition.y + rect.sizeDelta.y / 2 <= 240,
                    "Every quick-test button fits inside the startup panel.");
            }
            selector.StartCloneQuickTest();
            Check(Phase(selector) == "WaitingToStart" && !selector.IsComplete,
                "Clone shortcut also requires arming and an audio system.");
            selector.StartNeutralQuickTest();
            Check(Phase(selector) == "WaitingToStart" && !selector.IsComplete,
                "Shortcut cannot start without released-controller arming and an audio system.");
            Field("m_Phase").SetValue(selector, Enum.Parse(Field("m_Phase").FieldType, "QuickTestFailed"));
            typeof(VoiceModeSelector).GetMethod("RefreshChoices", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(selector, null);
            Check(!selector.IsComplete && Array.Exists(canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(),
                label => label.text == "Cancel session") && !Array.Exists(canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(),
                label => label.text == "Record again"), "Quick-test failure stays blocked without routing to clone enrollment.");
            Field("m_PerspectiveChosen").SetValue(selector, true);
            Field("m_Phase").SetValue(selector, Enum.Parse(Field("m_Phase").FieldType, "Choosing"));
            foreach (var chosenGender in new[] { NeutralVoiceProfile.Male, NeutralVoiceProfile.Female })
            {
                selector.SelectGeneric();
                Check(Phase(selector) == "ChoosingNeutral", "Gender selection comes first.");
                if (chosenGender == NeutralVoiceProfile.Male) selector.SelectNeutralMale(); else selector.SelectNeutralFemale();
                Check(Phase(selector) == "ChoosingNeutralVoice", "Gender does not start enrollment.");
                for (int i = 0; i < SessionConfig.NeutralVoiceCount; i++)
                {
                    selector.SelectNeutralVoiceOption(i);
                    Check(Phase(selector) == "ChoosingNeutralVoice" && (int)Field("m_SelectedVoiceOption").GetValue(selector) == i,
                        "All six gendered voices can be selected/switched while cards stay visible.");
                    Check(SessionConfig.NeutralProfile == gender && SessionConfig.NeutralVoiceOption == option,
                        "Pending card selection does not commit the session voice.");
                    selector.PreviewNeutralVoiceOption((i + 1) % SessionConfig.NeutralVoiceCount);
                    Check((int)Field("m_SelectedVoiceOption").GetValue(selector) == i, "Auditioning another voice preserves the selection.");
                    selector.ConfirmNeutralVoice();
                    Check(Phase(selector) == "ChoosingNeutralVoice", "No audio system cannot confirm readiness.");
                    selector.SelectNeutralVoiceOption(-1);
                    Check((int)Field("m_SelectedVoiceOption").GetValue(selector) == i, "Invalid choice does not change selection.");
                }
            }
            Debug.Log("[VoiceChoiceChecks] PASS: gender then voice, six options, switching/back, invalid choice and no-audio confirmation gate.");
        }
        finally
        {
            var selector = host.GetComponent<VoiceModeSelector>();
            if (selector != null && Field("m_CanvasGO").GetValue(selector) is GameObject canvas)
                UnityEngine.Object.DestroyImmediate(canvas);
            UnityEngine.Object.DestroyImmediate(host);
            SessionConfig.SelectNeutralVoice(gender, option); SessionConfig.Voice = voice;
        }
    }
}
