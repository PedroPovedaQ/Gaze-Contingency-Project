using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Radial success burst and tumbling confetti, with no physics or search colliders.</summary>
public sealed class TargetCrumble : MonoBehaviour
{
    public const int PieceCount = 100;
    public const int ConfettiCount = 120;
    public const float Duration = 1.8f;
    readonly Vector3[] m_Centers = new Vector3[PieceCount], m_Velocities = new Vector3[PieceCount], m_Axes = new Vector3[PieceCount];
    readonly Vector3[] m_Corners = new Vector3[PieceCount * 4], m_Vertices = new Vector3[PieceCount * 12];
    Mesh m_Mesh;
    Material m_Material;
    Mesh m_ConfettiMesh;
    Material[] m_ConfettiMaterials;
    static AudioClip s_BurstClip;
    AudioSource m_BurstAudio;
    bool m_AudioPaused;
    readonly Vector3[] m_ConfettiVertices = new Vector3[ConfettiCount * 8];
    readonly Vector3[] m_ConfettiVelocities = new Vector3[ConfettiCount];
    static readonly Color[] k_ConfettiColors =
    {
        new Color(1f, .8f, 0f), new Color(0f, .85f, 1f), new Color(1f, .2f, .5f),
        new Color(.65f, .35f, 1f), Color.white
    };
    FindObjectGameManager m_Game;
    float m_Time;
    static readonly int[] k_Faces = { 0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3 };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadBurstAudio() => s_BurstClip = Resources.Load<AudioClip>("ConfettiBurst");

    public static TargetCrumble Play(GameObject target, FindObjectGameManager game)
    {
        if (target == null || !target.TryGetComponent<Renderer>(out var source)) return null;
        var root = new GameObject("Correct target burst and confetti");
        root.transform.position = source.bounds.center;
        var effect = root.AddComponent<TargetCrumble>();
        effect.m_Game = game;
        effect.Create(target, source);
        effect.PlayBurstAudio();
        // Keep the actual object/metadata alive for capture observers and normal
        // round cleanup. Only its visible surface is replaced after timing stops.
        source.enabled = false;
        return effect;
    }

    void PlayBurstAudio()
    {
        if (s_BurstClip == null) LoadBurstAudio();
        if (s_BurstClip == null) return;
        m_BurstAudio = gameObject.AddComponent<AudioSource>();
        m_BurstAudio.playOnAwake = false;
        m_BurstAudio.clip = s_BurstClip;
        m_BurstAudio.spatialBlend = 1f;
        m_BurstAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        m_BurstAudio.minDistance = 1f;
        m_BurstAudio.maxDistance = 15f;
        m_BurstAudio.dopplerLevel = 0f;
        m_BurstAudio.volume = .55f;
        if (!Application.isPlaying) return;
        m_BurstAudio.Play();
        m_Game?.GetComponent<TrialReplayRecorder>()?.RecordEffect(s_BurstClip, "confetti_burst", m_BurstAudio.volume);
    }

    void Create(GameObject target, Renderer source)
    {
        var bounds = source.bounds;
        var collider = target.GetComponent<Collider>();
        var random = new System.Random(100); // Never disturb the experiment's RNG.
        float Next() => (float)random.NextDouble();
        float size = Mathf.Max(.002f, bounds.size.magnitude * .085f);
        for (int i = 0; i < PieceCount; i++)
        {
            var point = bounds.min + Vector3.Scale(bounds.size, new Vector3(Next(), Next(), Next()));
            // Pull outside samples onto the surface, preserving the selected shape's silhouette.
            if (collider != null && collider.enabled) point = collider.ClosestPoint(point);
            m_Centers[i] = point - bounds.center;
            // Even spherical coverage includes downward and backward pieces,
            // regardless of the selected object's shape or orientation.
            m_Velocities[i] = BurstDirection(i, PieceCount) * (.9f + Next() * .6f);
            m_Axes[i] = new Vector3(Next() + .1f, Next() + .1f, Next() + .1f).normalized;
            Quaternion rotation = Quaternion.Euler(Next() * 360, Next() * 360, Next() * 360);
            float radius = size * (.65f + Next() * .7f);
            m_Corners[i * 4] = rotation * new Vector3(1, 1, 1) * radius;
            m_Corners[i * 4 + 1] = rotation * new Vector3(-1, -1, 1) * radius;
            m_Corners[i * 4 + 2] = rotation * new Vector3(-1, 1, -1) * radius;
            m_Corners[i * 4 + 3] = rotation * new Vector3(1, -1, -1) * radius;
        }
        m_Mesh = new Mesh { name = "100 crumble fragments" }; m_Mesh.MarkDynamic();
        var indices = new int[m_Vertices.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        UpdateGeometry(0);
        m_Mesh.triangles = indices; m_Mesh.RecalculateNormals();
        gameObject.AddComponent<MeshFilter>().sharedMesh = m_Mesh;
        m_Material = new Material(source.sharedMaterial);
        var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = m_Material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        CreateConfetti(source.sharedMaterial, random);
    }

    static Vector3 BurstDirection(int index, int count)
    {
        float y = 1f - 2f * (index + .5f) / count;
        float radius = Mathf.Sqrt(1f - y * y);
        float angle = index * 2.39996323f; // Golden-angle sphere: no global RNG.
        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    void CreateConfetti(Material source, System.Random random)
    {
        var child = new GameObject("Success confetti");
        child.transform.SetParent(transform, false);
        m_ConfettiMesh = new Mesh { name = "Double-sided confetti" };
        m_ConfettiMesh.MarkDynamic();
        m_ConfettiMaterials = new Material[k_ConfettiColors.Length];
        for (int i = 0; i < ConfettiCount; i++)
            m_ConfettiVelocities[i] = BurstDirection(i, ConfettiCount) * (1.1f + (float)random.NextDouble() * .8f);
        UpdateConfetti(0);
        m_ConfettiMesh.subMeshCount = k_ConfettiColors.Length;
        for (int color = 0; color < k_ConfettiColors.Length; color++)
        {
            m_ConfettiMaterials[color] = new Material(source);
            m_ConfettiMaterials[color].SetColor("_BaseColor", k_ConfettiColors[color]);
            var triangles = new int[ConfettiCount / k_ConfettiColors.Length * 12];
            int cursor = 0;
            for (int i = color; i < ConfettiCount; i += k_ConfettiColors.Length)
            {
                int v = i * 8;
                triangles[cursor++] = v; triangles[cursor++] = v + 1; triangles[cursor++] = v + 2;
                triangles[cursor++] = v; triangles[cursor++] = v + 2; triangles[cursor++] = v + 3;
                triangles[cursor++] = v + 4; triangles[cursor++] = v + 6; triangles[cursor++] = v + 5;
                triangles[cursor++] = v + 4; triangles[cursor++] = v + 7; triangles[cursor++] = v + 6;
            }
            m_ConfettiMesh.SetTriangles(triangles, color);
        }
        m_ConfettiMesh.RecalculateNormals();
        child.AddComponent<MeshFilter>().sharedMesh = m_ConfettiMesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = m_ConfettiMaterials;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    void UpdateConfetti(float t)
    {
        float travel = (1f - Mathf.Exp(-2f * t)) / 2f;
        float shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, Duration, t));
        for (int i = 0; i < ConfettiCount; i++)
        {
            Vector3 center = m_ConfettiVelocities[i] * travel + Vector3.down * (.3f * t * t);
            center.x += Mathf.Sin(i + t * 11f) * .035f * t;
            Quaternion spin = Quaternion.Euler(i * 31f + t * 390f, i * 17f + t * 210f, i * 43f + t * 170f);
            float width = (.012f + (i % 4) * .003f) * shrink;
            Vector3 right = spin * Vector3.right * width;
            Vector3 up = spin * Vector3.up * width * .45f;
            int v = i * 8;
            m_ConfettiVertices[v] = center - right - up;
            m_ConfettiVertices[v + 1] = center + right - up;
            m_ConfettiVertices[v + 2] = center + right + up;
            m_ConfettiVertices[v + 3] = center - right + up;
            for (int j = 0; j < 4; j++) m_ConfettiVertices[v + 4 + j] = m_ConfettiVertices[v + j];
        }
        m_ConfettiMesh.vertices = m_ConfettiVertices;
        m_ConfettiMesh.RecalculateBounds(); m_ConfettiMesh.RecalculateNormals();
    }

    // Fast spherical burst, tumbling drift, then shrink away. The independent root
    // survives removal of the selected object, including the final trial.
    void UpdateGeometry(float seconds)
    {
        for (int i = 0; i < PieceCount; i++)
        {
            float t = Mathf.Max(0, seconds);
            float shrink = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1.25f, t));
            float travel = (1f - Mathf.Exp(-2f * t)) / 2f;
            Vector3 center = m_Centers[i] + m_Velocities[i] * travel + Vector3.down * (.25f * t * t);
            Quaternion tumble = Quaternion.AngleAxis(t * (140 + i * 3), m_Axes[i]);
            for (int j = 0; j < 12; j++)
                m_Vertices[i * 12 + j] = center + tumble * m_Corners[i * 4 + k_Faces[j]] * shrink;
        }
        m_Mesh.vertices = m_Vertices;
        m_Mesh.RecalculateBounds(); m_Mesh.RecalculateNormals();
        if (m_ConfettiMesh != null) UpdateConfetti(seconds);
    }
    void Update()
    {
        if (m_Game != null && m_Game.ResearcherPaused)
        {
            if (!m_AudioPaused && m_BurstAudio != null) m_BurstAudio.Pause();
            m_AudioPaused = true;
            return;
        }
        if (m_AudioPaused && m_BurstAudio != null) m_BurstAudio.UnPause();
        m_AudioPaused = false;
        m_Time += Time.deltaTime;
        if (m_Time >= Duration) { Destroy(gameObject); return; }
        UpdateGeometry(m_Time);
    }
    void OnDestroy()
    {
        Release(m_Mesh); Release(m_Material); Release(m_ConfettiMesh);
        if (m_ConfettiMaterials != null)
            foreach (var material in m_ConfettiMaterials) Release(material);
    }

    static void Release(Object resource)
    {
        if (resource == null) return;
        if (Application.isPlaying) Destroy(resource);
        else DestroyImmediate(resource);
    }
}
