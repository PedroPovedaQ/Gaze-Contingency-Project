using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Logs per-frame gaze pose and hover events to a CSV file.
/// Runs regardless of whether the gaze ray visual is enabled.
/// </summary>
public class GazeDataLogger : MonoBehaviour
{
    const string k_Tag = "[GazeLog]";
    const int k_FlushInterval = 300; // flush every N frames

    XRBaseInputInteractor m_Interactor;
    StreamWriter m_Writer;
    string m_CurrentHoveredObject = "";
    string m_CurrentHoveredShape = "";
    string m_CurrentHoveredColor = "";
    int m_FrameCount;

    void OnEnable()
    {
        m_Interactor = GetComponent<XRBaseInputInteractor>();
        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.AddListener(OnHoverEntered);
            m_Interactor.hoverExited.AddListener(OnHoverExited);
        }

        var dir = Application.persistentDataPath;
        var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(dir, $"gaze_log_{timestamp}.csv");

        m_Writer = new StreamWriter(path, false, Encoding.UTF8);
        m_Writer.WriteLine("timestamp,frame,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,hovered_object,hovered_shape,hovered_color,ray_visible");
        m_Writer.Flush();

        Debug.Log($"{k_Tag} Logging to {path}");
    }

    void OnDisable()
    {
        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.RemoveListener(OnHoverEntered);
            m_Interactor.hoverExited.RemoveListener(OnHoverExited);
        }

        if (m_Writer != null)
        {
            m_Writer.Flush();
            m_Writer.Close();
            m_Writer = null;
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        m_CurrentHoveredObject = args.interactableObject.transform.name;

        var info = args.interactableObject.transform.GetComponent<SpawnableObjectInfo>();
        if (info != null)
        {
            m_CurrentHoveredShape = info.shapeName;
            m_CurrentHoveredColor = info.colorName;
        }
        else
        {
            m_CurrentHoveredShape = "";
            m_CurrentHoveredColor = "";
        }
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (m_CurrentHoveredObject == args.interactableObject.transform.name)
        {
            m_CurrentHoveredObject = "";
            m_CurrentHoveredShape = "";
            m_CurrentHoveredColor = "";
        }
    }

    void Update()
    {
        if (m_Writer == null) return;

        var t = transform;
        var pos = t.position;
        var rot = t.rotation.eulerAngles;
        var lineVisual = GetComponent<XRInteractorLineVisual>();
        bool rayVisible = lineVisual != null && lineVisual.enabled;

        m_Writer.WriteLine(string.Format("{0:F3},{1},{2:F4},{3:F4},{4:F4},{5:F2},{6:F2},{7:F2},{8},{9},{10},{11}",
            Time.time, Time.frameCount,
            pos.x, pos.y, pos.z,
            rot.x, rot.y, rot.z,
            m_CurrentHoveredObject,
            m_CurrentHoveredShape,
            m_CurrentHoveredColor,
            rayVisible ? 1 : 0));

        m_FrameCount++;
        if (m_FrameCount >= k_FlushInterval)
        {
            m_Writer.Flush();
            m_FrameCount = 0;
        }
    }
}
