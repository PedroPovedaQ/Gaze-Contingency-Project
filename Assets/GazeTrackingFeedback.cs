using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>Automatic, non-interactive status card using the same gaze validity as guidance.</summary>
[DefaultExecutionOrder(300)]
public sealed class GazeTrackingFeedback : MonoBehaviour
{
    readonly GazeTrackingFeedbackState m_State = new();
    GameObject m_Card;
    TextMeshProUGUI m_Text;
    double m_EnabledAt;
    bool m_Paused;
    string m_LastMessage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Attach()
    {
        // Use the scene spawner, just like game-manager bootstrap: AfterSceneLoad
        // callbacks have no guaranteed order. Missing eye streams must also be reported.
        if (FindFirstObjectByType<ObjectSpawner>() != null &&
            FindFirstObjectByType<GazeTrackingFeedback>() == null)
            new GameObject("Eye tracking feedback").AddComponent<GazeTrackingFeedback>();
    }

    void OnEnable() { m_EnabledAt = Time.realtimeSinceStartupAsDouble; }
    void OnApplicationPause(bool paused) { m_Paused = paused; if (paused) Hide(); }
    void OnApplicationFocus(bool focused) { if (!focused) Hide(); }
    void OnDisable() => Hide();
    void Hide()
    {
        m_State.Reset(); m_LastMessage = null;
        if (m_Card != null) m_Card.SetActive(false);
    }

    void LateUpdate()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        var head = Camera.main;
        var stream = SharedGazeStream.Instance;
        bool usable = stream != null && stream.TryGetValid(out _);
        // Entry alignment owns its tracking feedback and Skip button; never cover them.
        bool eligible = !EntryGazeAlignment.IsShowing && !m_Paused && Application.isFocused && head != null && now - m_EnabledAt >= 2;
        m_State.Step(eligible, usable, now);
        if (!m_State.Visible)
        {
            if (m_LastMessage != null) Debug.Log("[EyeTrackingFeedback] " + (usable ? "Tracking recovered." : "Feedback hidden; application inactive."));
            m_LastMessage = null;
            if (m_Card != null) m_Card.SetActive(false);
            return;
        }
        var sample = stream != null ? stream.Latest : null;
        bool fresh = sample != null && GazeQualityPolicy.IsFresh(true, sample.inputEventTime, now, sample.maxDeliveryAgeSeconds);
        bool nativeFresh = sample != null && sample.htcGazeAvailable &&
            GazeQualityPolicy.IsFresh(true, sample.htcQueryReceiptTime, now, sample.maxDeliveryAgeSeconds);
        string reason = GazeTrackingFeedbackState.Reason(stream != null && stream.isActiveAndEnabled && sample != null && sample.deviceAvailable,
            fresh, nativeFresh, sample != null && sample.leftValid, sample != null && sample.rightValid,
            sample != null && sample.headValid);
        string message = usable ? "<b>Eye tracking is recovering...</b>" :
            "<color=#FFC904><b>Eye tracking unavailable</b></color>\n" + reason +
            "\nAdjust the headset so both eyes are unobstructed.\nIf this continues, run the Vive eye calibration.";
        if (m_Card == null) CreateCard();
        if (message != m_LastMessage)
        {
            m_Text.text = message; m_LastMessage = message;
            Debug.Log("[EyeTrackingFeedback] " + (usable ? "Confirming stable recovery." : reason));
        }
        m_Card.SetActive(true);
        // Non-interactive notice follows below view center; it cannot intercept controller input.
        m_Card.transform.SetPositionAndRotation(head.transform.TransformPoint(new Vector3(0, -.32f, 1.2f)), head.transform.rotation);
    }

    void CreateCard()
    {
        m_Card = StudyPromptCard.Create(null, "Eye tracking unavailable", new Vector2(800, 220));
        m_Card.transform.localScale = Vector3.one * .001f;
        var canvas = m_Card.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 100;
        m_Card.GetComponent<Image>().raycastTarget = false;
        var label = new GameObject("Tracking status", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(m_Card.transform, false);
        m_Text = label.GetComponent<TextMeshProUGUI>();
        m_Text.rectTransform.sizeDelta = new Vector2(750, 190);
        StudyPromptCard.StyleText(m_Text, 27);
    }

    void OnDestroy() { if (m_Card != null) Destroy(m_Card); }
}
