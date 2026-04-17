using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates two traditional bookshelf units on the detected table and computes
/// designated spawn points for objects. Layout is computed once and cached
/// so spawn points are consistent across rounds.
/// </summary>
public static class ShelfSpawner
{
    const int k_Rows = 7;
    const int k_Cols = 2;
    const float k_MinBookcaseWidth = 0.30f;
    const float k_BookcaseHeight = 0.84f;  // 7 rows × 12cm each
    const float k_BookcaseDepth = 0.18f;
    const float k_ColGap = 0.03f;
    const float k_DepthSetback = 0.06f;
    const float k_PlankThickness = 0.012f;
    const float k_PanelThickness = 0.012f;

    static float RowHeight => k_BookcaseHeight / k_Rows;

    public static int RowCount => k_Rows;
    public static int ColCount => k_Cols;

    /// <summary>Rotation objects should have to face straight out of the shelf.</summary>
    public static Quaternion ObjectFacingRotation { get; private set; } = Quaternion.identity;

    /// <summary>The shelf's right axis in world space (along bookcase width).</summary>
    public static Vector3 ShelfRight { get; private set; } = Vector3.right;

    /// <summary>The direction the shelf opens toward (toward the player).</summary>
    public static Vector3 ShelfFacing { get; private set; } = Vector3.forward;

    public struct SpawnPoint
    {
        public Vector3 position;
        public int row;
        public int col;
    }

    // Cached layout — computed once in CreateShelvesAndSpawnPoints, reused by ComputeSpawnPoints
    static bool s_LayoutCached;
    static Vector3[] s_ColCenters;
    static Vector3 s_ShelfRight;
    static Vector3 s_ShelfForward;
    static float s_BookcaseWidth;
    static float s_TableY;

    /// <summary>Reuse cached layout to get spawn points. Call for rounds after the first.</summary>
    public static List<SpawnPoint> ComputeSpawnPoints(
        Vector3 tableCenter, Vector2 tableSize,
        Vector3 planeRight, Vector3 planeForward,
        int totalObjects)
    {
        if (!s_LayoutCached)
        {
            // Fallback: compute fresh (shouldn't happen if CreateShelvesAndSpawnPoints was called first)
            ComputeLayout(tableCenter, tableSize, planeRight, planeForward,
                out _, out var sr, out var sf, out var cc, out float bw);
            CacheLayout(sr, sf, cc, bw, tableCenter.y);
        }
        return BuildSpawnPoints(s_ColCenters, s_ShelfRight, s_ShelfForward,
            s_BookcaseWidth, s_TableY, totalObjects);
    }

    /// <summary>Build shelf geometry AND compute spawn points. Call once at game start.</summary>
    public static (List<GameObject> shelfObjects, List<SpawnPoint> spawnPoints)
        CreateShelvesAndSpawnPoints(
            Vector3 tableCenter, Vector2 tableSize,
            Vector3 planeRight, Vector3 planeForward,
            int totalObjects)
    {
        ComputeLayout(tableCenter, tableSize, planeRight, planeForward,
            out var baseCenter, out var shelfRight, out var shelfForward,
            out var colCenters, out float bookcaseWidth);

        CacheLayout(shelfRight, shelfForward, colCenters, bookcaseWidth, tableCenter.y);

        // Build geometry
        var shelfObjects = new List<GameObject>();
        for (int col = 0; col < k_Cols; col++)
        {
            var parts = BuildBookcase(colCenters[col], shelfRight, shelfForward, col, bookcaseWidth);
            shelfObjects.AddRange(parts);
        }

        var spawnPoints = BuildSpawnPoints(colCenters, shelfRight, shelfForward,
            bookcaseWidth, tableCenter.y, totalObjects);

        Debug.Log($"[ShelfSpawner] Built {k_Cols} bookcases ({bookcaseWidth:F2}m wide), " +
                  $"{k_Rows} rows, {spawnPoints.Count} spawn points");
        return (shelfObjects, spawnPoints);
    }

    /// <summary>Clears cached layout. Call on full game reset.</summary>
    public static void ClearCache() { s_LayoutCached = false; }

    // Keep old signature for compat
    public static (List<GameObject>, List<List<Vector3>>) CreateShelvesAndPositions(
        Vector3 tableCenter, Vector2 tableSize, Vector3 planeRight, Vector3 planeForward,
        int totalObjects, bool buildGeometry = true)
    {
        if (buildGeometry)
        {
            var (objs, pts) = CreateShelvesAndSpawnPoints(tableCenter, tableSize, planeRight, planeForward, totalObjects);
            return (objs, SpawnPointsToLevels(pts));
        }
        var sp = ComputeSpawnPoints(tableCenter, tableSize, planeRight, planeForward, totalObjects);
        return (new List<GameObject>(), SpawnPointsToLevels(sp));
    }

    static List<List<Vector3>> SpawnPointsToLevels(List<SpawnPoint> pts)
    {
        var levels = new List<List<Vector3>>();
        for (int r = 0; r < k_Rows; r++) levels.Add(new List<Vector3>());
        foreach (var sp in pts)
            if (sp.row < levels.Count) levels[sp.row].Add(sp.position);
        return levels;
    }

    static void CacheLayout(Vector3 shelfRight, Vector3 shelfForward,
        Vector3[] colCenters, float bookcaseWidth, float tableY)
    {
        s_ShelfRight = shelfRight;
        s_ShelfForward = shelfForward;
        s_ColCenters = colCenters;
        s_BookcaseWidth = bookcaseWidth;
        s_TableY = tableY;
        s_LayoutCached = true;
    }

    // =====================================================================
    //  Layout computation
    // =====================================================================

    static void ComputeLayout(
        Vector3 tableCenter, Vector2 tableSize,
        Vector3 planeRight, Vector3 planeForward,
        out Vector3 baseCenter, out Vector3 shelfRight, out Vector3 shelfForward,
        out Vector3[] colCenters, out float bookcaseWidth)
    {
        // Snap shelf facing to nearest table axis relative to the player
        Vector3 facingDir = -planeForward;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 toPlayer = cam.transform.position - tableCenter;
            toPlayer.y = 0;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                float dotFwd = Vector3.Dot(toPlayer.normalized, planeForward);
                float dotRight = Vector3.Dot(toPlayer.normalized, planeRight);
                if (Mathf.Abs(dotFwd) >= Mathf.Abs(dotRight))
                    facingDir = dotFwd > 0 ? planeForward : -planeForward;
                else
                    facingDir = dotRight > 0 ? planeRight : -planeRight;
            }
        }

        shelfRight = Vector3.Cross(Vector3.up, facingDir).normalized;
        if (shelfRight.sqrMagnitude < 0.001f) shelfRight = planeRight;
        shelfForward = -facingDir;

        ObjectFacingRotation = Quaternion.LookRotation(facingDir, Vector3.up);
        ShelfRight = shelfRight;
        ShelfFacing = facingDir;

        float tableW = Mathf.Max(
            Mathf.Abs(Vector3.Dot(planeRight * tableSize.x, shelfRight)),
            Mathf.Abs(Vector3.Dot(planeForward * tableSize.y, shelfRight)));
        if (tableW < 0.2f) tableW = tableSize.x;

        bookcaseWidth = Mathf.Max((tableW - k_ColGap - 0.04f) / k_Cols, k_MinBookcaseWidth);
        float totalWidth = k_Cols * bookcaseWidth + (k_Cols - 1) * k_ColGap;

        baseCenter = tableCenter + Vector3.up * 0.001f + shelfForward * k_DepthSetback;

        colCenters = new Vector3[k_Cols];
        for (int col = 0; col < k_Cols; col++)
        {
            float xOff = -totalWidth / 2f + bookcaseWidth / 2f + col * (bookcaseWidth + k_ColGap);
            colCenters[col] = baseCenter + shelfRight * xOff;
        }
    }

    // =====================================================================
    //  Spawn points
    // =====================================================================

    static List<SpawnPoint> BuildSpawnPoints(
        Vector3[] colCenters, Vector3 shelfRight, Vector3 shelfForward,
        float bookcaseWidth, float tableY, int totalObjects)
    {
        float innerWidth = bookcaseWidth - 2 * k_PanelThickness;
        float rowH = RowHeight;
        int slotCount = k_Rows * k_Cols;
        int perSlot = totalObjects / slotCount;
        int remainder = totalObjects % slotCount;
        int pairedExtraRows = remainder / k_Cols;
        int leftoverSingles = remainder % k_Cols;
        var rowsWithExtraPair = BuildExtraRowMask(pairedExtraRows);

        var points = new List<SpawnPoint>();

        for (int row = 0; row < k_Rows; row++)
        {
            // Half-height of objects after 20% size reduction (8cm objects → 4cm center)
            float y = tableY + row * rowH + k_PlankThickness + 0.04f;

            for (int col = 0; col < k_Cols; col++)
            {
                int count = perSlot;
                if (rowsWithExtraPair[row])
                    count++;
                if (leftoverSingles > 0 && row == k_Rows / 2 && col < leftoverSingles)
                    count++;

                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0.5f : (float)(i + 1) / (count + 1);
                    float localX = Mathf.Lerp(-innerWidth / 2f, innerWidth / 2f, t);
                    float localZ = -k_BookcaseDepth * 0.15f;

                    Vector3 pos = colCenters[col]
                        + shelfRight * localX
                        + shelfForward * localZ
                        + Vector3.up * (y - tableY);

                    points.Add(new SpawnPoint { position = pos, row = row, col = col });
                }
            }
        }

        return points;
    }

    static bool[] BuildExtraRowMask(int pairedExtraRows)
    {
        var mask = new bool[k_Rows];
        if (pairedExtraRows <= 0)
            return mask;

        pairedExtraRows = Mathf.Min(pairedExtraRows, k_Rows);

        // Spread denser rows across the shelf so the extra left/right objects stay
        // visually balanced instead of all accumulating in the same band.
        for (int i = 0; i < pairedExtraRows; i++)
        {
            int row = Mathf.Clamp(
                Mathf.FloorToInt((i + 0.5f) * k_Rows / pairedExtraRows),
                0,
                k_Rows - 1);
            mask[row] = true;
        }

        return mask;
    }

    // =====================================================================
    //  Bookcase geometry
    // =====================================================================

    static List<GameObject> BuildBookcase(Vector3 center, Vector3 right, Vector3 forward, int colIndex, float width)
    {
        var parts = new List<GameObject>();
        var parent = new GameObject($"Bookcase_Col{colIndex}");
        parent.transform.position = center;

        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);
        float rowH = RowHeight;
        float innerWidth = width - 2 * k_PanelThickness;

        for (int side = 0; side < 2; side++)
        {
            float xSign = side == 0 ? -1f : 1f;
            float xPos = xSign * (width / 2f - k_PanelThickness / 2f);
            Vector3 pos = center + right * xPos + Vector3.up * (k_BookcaseHeight / 2f);
            parts.Add(CreateBox($"Side_{colIndex}_{side}", pos, rot,
                new Vector3(k_PanelThickness, k_BookcaseHeight, k_BookcaseDepth), parent.transform));
        }

        {
            float zPos = k_BookcaseDepth / 2f - k_PanelThickness / 2f;
            Vector3 pos = center + forward * zPos + Vector3.up * (k_BookcaseHeight / 2f);
            parts.Add(CreateBox($"Back_{colIndex}", pos, rot,
                new Vector3(width, k_BookcaseHeight, k_PanelThickness), parent.transform));
        }

        for (int p = 0; p <= k_Rows; p++)
        {
            float yPos = p * rowH + k_PlankThickness / 2f;
            Vector3 pos = center + Vector3.up * yPos;
            // Top plank spans full width (sits on top of side panels)
            float plankWidth = p == k_Rows ? width : innerWidth;
            parts.Add(CreateBox($"Plank_{colIndex}_{p}", pos, rot,
                new Vector3(plankWidth, k_PlankThickness, k_BookcaseDepth), parent.transform));
        }

        parts.Add(parent);
        return parts;
    }

    static GameObject CreateBox(string name, Vector3 position, Quaternion rotation,
        Vector3 scale, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = scale;

        var mf = go.AddComponent<MeshFilter>();
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);

        var renderer = go.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Shader Graphs/InteractablePrimitive");
        var mat = new Material(shader);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.SetFloat("_Surface", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = -1;
        mat.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.94f, 1f));
        mat.color = new Color(0.92f, 0.92f, 0.94f, 1f);
        renderer.material = mat;

        return go;
    }
}
