using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utility that creates virtual shelf platforms above a detected table
/// and computes grid spawn positions per level for object distribution.
/// </summary>
public static class ShelfSpawner
{
    const float k_ShelfSpacing = 0.25f;    // 25cm between levels
    const float k_ShelfThickness = 0.015f; // 1.5cm visual thickness
    const float k_ShelfInset = 0.03f;      // 3cm inset from table edges
    const float k_ShelfAlpha = 0.3f;       // semi-transparent
    const float k_Jitter = 0.02f;          // ±2cm random jitter
    const float k_SpawnHeightOffset = 0.05f;
    const float k_Margin = 0.05f;

    /// <summary>
    /// Creates shelf platforms and computes spawn positions across all levels.
    /// </summary>
    /// <param name="tableCenter">World-space center of the detected table plane.</param>
    /// <param name="tableSize">Width/height of the table plane.</param>
    /// <param name="planeRight">Table's local right axis in world space.</param>
    /// <param name="planeForward">Table's local forward axis in world space.</param>
    /// <param name="totalObjects">Total number of objects to distribute.</param>
    /// <returns>
    /// shelfObjects: GameObjects to destroy on cleanup.
    /// positionsPerLevel: list of position lists indexed by level (0=table, 1=lower shelf, 2=upper shelf).
    /// levelPerPosition: flat list of level indices parallel to the flattened positions.
    /// </returns>
    public static (List<GameObject> shelfObjects, List<List<Vector3>> positionsPerLevel)
        CreateShelvesAndPositions(
            Vector3 tableCenter, Vector2 tableSize,
            Vector3 planeRight, Vector3 planeForward,
            int totalObjects)
    {
        var shelfObjects = new List<GameObject>();
        int levelCount = 3;

        // Create shelf platforms for levels 1 and 2 (level 0 is the table itself)
        for (int level = 1; level < levelCount; level++)
        {
            float yOffset = level * k_ShelfSpacing;
            Vector3 shelfCenter = tableCenter + Vector3.up * yOffset;

            float shelfW = tableSize.x - k_ShelfInset * 2;
            float shelfH = tableSize.y - k_ShelfInset * 2;

            var shelfGO = new GameObject($"VirtualShelf_Level{level}");
            shelfGO.transform.position = shelfCenter;
            shelfGO.transform.rotation = Quaternion.identity;

            // Box collider for physics (objects land on this)
            var col = shelfGO.AddComponent<BoxCollider>();
            col.size = new Vector3(shelfW, k_ShelfThickness, shelfH);

            // Add physics material with friction to prevent sliding
            var physicMat = new PhysicsMaterial("ShelfFriction")
            {
                staticFriction = 0.8f,
                dynamicFriction = 0.6f,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };
            col.material = physicMat;

            // Visual mesh — simple cube scaled to shelf dimensions
            var meshFilter = shelfGO.AddComponent<MeshFilter>();
            var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshFilter.sharedMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tempCube);

            var renderer = shelfGO.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // transparent
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.color = new Color(0.8f, 0.8f, 0.9f, k_ShelfAlpha);
            renderer.material = mat;

            shelfGO.transform.localScale = new Vector3(shelfW, k_ShelfThickness, shelfH);

            shelfObjects.Add(shelfGO);
            Debug.Log($"[ShelfSpawner] Created shelf level {level} at y={shelfCenter.y:F2}, size=({shelfW:F2}, {shelfH:F2})");
        }

        // Distribute objects across levels
        int perLevel = totalObjects / levelCount;
        int remainder = totalObjects % levelCount;
        var objectsPerLevel = new int[levelCount];
        for (int i = 0; i < levelCount; i++)
        {
            objectsPerLevel[i] = perLevel + (i < remainder ? 1 : 0);
        }

        // Compute grid positions per level
        var positionsPerLevel = new List<List<Vector3>>();
        for (int level = 0; level < levelCount; level++)
        {
            float yBase = tableCenter.y + level * k_ShelfSpacing;
            int count = objectsPerLevel[level];
            var positions = ComputeGridPositions(
                tableCenter, tableSize, planeRight, planeForward,
                yBase, count);
            positionsPerLevel.Add(positions);
        }

        return (shelfObjects, positionsPerLevel);
    }

    static List<Vector3> ComputeGridPositions(
        Vector3 center, Vector2 tableSize,
        Vector3 planeRight, Vector3 planeForward,
        float yBase, int count)
    {
        if (count <= 0) return new List<Vector3>();

        float usableW = tableSize.x - k_Margin * 2;
        float usableH = tableSize.y - k_Margin * 2;

        // Determine grid dimensions
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / cols);

        float cellW = usableW / cols;
        float cellH = usableH / rows;

        var positions = new List<Vector3>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (positions.Count >= count) break;

                float localX = -usableW / 2f + cellW * (col + 0.5f);
                float localY = -usableH / 2f + cellH * (row + 0.5f);

                localX += Random.Range(-k_Jitter, k_Jitter);
                localY += Random.Range(-k_Jitter, k_Jitter);

                Vector3 worldPos = center
                    + planeRight * localX
                    + planeForward * localY;
                worldPos.y = yBase + k_SpawnHeightOffset;

                positions.Add(worldPos);
            }
        }

        // Shuffle positions within this level
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        return positions;
    }
}
