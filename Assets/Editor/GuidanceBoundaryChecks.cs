using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GuidanceBoundaryChecks
{
    static FieldInfo Field(string name) => typeof(FindObjectGameManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    [MenuItem("Tools/Codex/Check Guidance Boundary")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var host = new GameObject("BoundaryFixture"); host.SetActive(false);
        var menu = new GameObject("WristToggleFixture", typeof(RectTransform), typeof(Toggle)); menu.SetActive(false);
        var target = new GameObject("SearchObjectFixture");
        FindObjectGameManager game = null;
        List<GameObject> guides = null;
        try
        {
            game = host.AddComponent<FindObjectGameManager>(); game.enabled = false;
            host.SetActive(true); // Discoverable by the real wrist binding without starting the game.
            game.SetDegreeGuidesVisible(false);
            typeof(FindObjectGameManager).GetMethod("CreateRotationalFrames", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
            guides = (List<GameObject>)Field("m_DegreeGuides").GetValue(game);
            Check(guides.Count == 8 && guides.TrueForAll(g => !g.activeSelf && g.GetComponentInChildren<TextMeshPro>(true) != null),
                "Hidden preference applies to all eight newly created walls and labels");
            for (int plane = 0; plane < 8; plane++)
                foreach (float pitch in new[] { -89f, -45f, 0f, 45f, 89f })
                {
                    Vector3 direction = Quaternion.Euler(pitch, plane * 45f, 0) * Vector3.forward;
                    Check(game.IsGazeInSearchSector(new Vector3(0, 10, 0), direction, plane, out bool valid) && valid,
                        "All sectors ignore gaze height and vertical scan pitch");
                }
            Check(!game.IsGazeInSearchSector(Vector3.zero, Vector3.up, 0, out bool verticalValid) && !verticalValid,
                "Vertical gaze has no fabricated exit");
            Check(!game.IsGazeInSearchSector(Vector3.zero, Vector3.right, 0, out bool otherValid) && otherValid,
                "Different horizontal wall is a real exit");
            var binding = menu.AddComponent<DegreeGuidesToggle>(); menu.SetActive(true);
            typeof(DegreeGuidesToggle).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(binding, null);
            var toggle = menu.GetComponent<Toggle>();
            Check(!toggle.isOn && toggle.interactable, "Wrist toggle reflects initial hidden preference");
            Check(menu.GetComponent<RayMenuFeedback>() != null, "Wrist control has shared ray feedback");
            bool inside = game.IsInsideSearchPlane(new Vector3(0, 1, 2), 0);
            toggle.isOn = true;
            Check(game.DegreeGuidesVisible && guides.TrueForAll(g => g.activeSelf), "Wrist switch shows walls and degrees");
            toggle.isOn = false;
            Check(!game.DegreeGuidesVisible && guides.TrueForAll(g => !g.activeSelf), "Wrist switch hides walls and degrees");
            Check(target.activeSelf && inside == game.IsInsideSearchPlane(new Vector3(0, 1, 2), 0), "Visual toggle leaves objects and zone geometry active");
            menu.SetActive(false); game.SetDegreeGuidesVisible(true); menu.SetActive(true);
            typeof(DegreeGuidesToggle).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(binding, null);
            Check(toggle.isOn, "Reopened wrist reflects public API changes");
            game.SetDegreeGuidesVisible(false);
            Check(!toggle.isOn, "Open wrist stays synchronized with public API changes");
            Debug.Log("[GuidanceBoundaryChecks] PASS: all eight walls/labels, pre-spawn preference, wrist on/off, reopening/API sync, ray feedback and unchanged zone geometry.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(menu);
            if (guides != null) foreach (var guide in guides) UnityEngine.Object.DestroyImmediate(guide);
            if (game != null && Field("m_PlaneOutlineMaterial").GetValue(game) is Material material)
            {
                UnityEngine.Object.DestroyImmediate(material);
                Field("m_PlaneOutlineMaterial").SetValue(game, null);
            }
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
