using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    [Serializable] sealed class NeutralAudioStatus { public string stage, error; public int completed, total; public float seconds; public bool ok; }
    Process m_NeutralAudioJob;
    NeutralAudioStatus m_NeutralAudioStatus;
    double m_NeutralAudioPoll;
    string NeutralAudioStatusPath => Path.Combine(m_LiveOutput, "neutral-audio-status.json");
    void NeutralAudioControls()
    {
        if(m_CaptureMode) return;
        bool running = m_NeutralAudioJob != null && !m_NeutralAudioJob.HasExited;
        if (Time.realtimeSinceStartupAsDouble > m_NeutralAudioPoll)
        {
            m_NeutralAudioPoll = Time.realtimeSinceStartupAsDouble + .5;
            try
            {
                if (File.Exists(NeutralAudioStatusPath)) m_NeutralAudioStatus = JsonUtility.FromJson<NeutralAudioStatus>(File.ReadAllText(NeutralAudioStatusPath));
                if (m_NeutralAudioJob != null && !running)
                {
                    if (m_NeutralAudioJob.ExitCode != 0 && string.IsNullOrEmpty(m_NeutralAudioStatus?.error))
                        m_NeutralAudioStatus = new NeutralAudioStatus { error = "Preparation stopped. Check connection and retry." };
                    m_NeutralAudioJob.Dispose(); m_NeutralAudioJob = null;
                }
            }
            catch (IOException) { /* Atomic status may be replaced during this poll. */ }
        }
        GUILayout.BeginHorizontal();
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && ReceiverConnected && !m_DetachedReview && !running;
        if (Button("Prepare neutral test audio", GUILayout.Width(210)))
        {
            try
            {
                Directory.CreateDirectory(m_LiveOutput); File.Delete(NeutralAudioStatusPath);
                m_NeutralAudioStatus = new NeutralAudioStatus { stage = "Starting neutral preparation…" };
                m_NeutralAudioJob = Process.Start(new ProcessStartInfo("/usr/bin/python3",
                    Argument(Path.Combine(Application.streamingAssetsPath, "neutral_audio.py")) +
                    " --endpoint " + Argument(m_Endpoint.Trim()) + " --output " + Argument(NeutralAudioStatusPath))
                    { UseShellExecute = false, CreateNoWindow = true });
            }
            catch (Exception e) { m_NeutralAudioStatus = new NeutralAudioStatus { error = "Could not start preparation: " + e.Message }; }
        }
        GUI.enabled = enabled;
        var status = m_NeutralAudioStatus;
        GUILayout.Label(status == null ? "Prepare Eric on this Mac before starting a headset quick test" :
            !string.IsNullOrEmpty(status.error) ? status.error : status.stage + (status.total > 0 ? $" · {status.completed}/{status.total}" : ""), m_MutedLabel);
        GUILayout.EndHorizontal();
    }
}
