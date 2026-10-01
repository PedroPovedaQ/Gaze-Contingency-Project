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
            UnityEngine.Object.DestroyImmediate(host);
            SessionConfig.SelectNeutralVoice(gender, option); SessionConfig.Voice = voice;
        }
    }
}
