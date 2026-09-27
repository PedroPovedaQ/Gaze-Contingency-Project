using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VoiceChoiceChecks
{
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
            var selector = host.AddComponent<VoiceModeSelector>();
            Field("m_PerspectiveChosen").SetValue(selector, true);
            Field("m_Phase").SetValue(selector, Enum.Parse(Field("m_Phase").FieldType, "Choosing"));
            foreach (var chosenGender in new[] { NeutralVoiceProfile.Male, NeutralVoiceProfile.Female })
            {
                selector.SelectGeneric();
                Check(Phase(selector) == "ChoosingNeutral", "Gender selection comes first.");
                if (chosenGender == NeutralVoiceProfile.Male) selector.SelectNeutralMale(); else selector.SelectNeutralFemale();
                Check(Phase(selector) == "ChoosingNeutralVoice", "Gender does not start enrollment.");
                for (int i = 0; i < 2; i++)
                {
                    selector.SelectNeutralVoiceOption(i);
                    Check(Phase(selector) == "PreviewingNeutral" && SessionConfig.NeutralProfile == chosenGender &&
                        SessionConfig.NeutralVoiceOption == i, "Both gendered voices can be selected/switched.");
                    selector.ConfirmNeutralVoice();
                    Check(Phase(selector) == "PreviewingNeutral", "No audio system cannot confirm readiness.");
                    selector.SelectNeutralVoiceOption(-1);
                    Check(SessionConfig.NeutralVoiceOption == i, "Invalid choice does not change selection.");
                }
            }
            Debug.Log("[VoiceChoiceChecks] PASS: gender then voice, both options, switching/back, invalid choice and no-audio confirmation gate.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            SessionConfig.SelectNeutralVoice(gender, option); SessionConfig.Voice = voice;
        }
    }
}
