using UnityEngine;

/*
 * WaterGridRuntime
 * - Generates a grid mesh at runtime (no ProBuilder needed).
 * - Samples WaveField.Instance for heights and approximate normals.
 * - Designed for small, calm chop. Scales to medium areas.
 *
 * Usage:
 * 1) Create Empty → name "Water".
 * 2) Add this component. It auto-adds MeshFilter/Renderer.
 * 3) Assign a URP/Lit material (teal), or any lit shader.
 * 4) Ensure exactly one WaveField exists in the scene.
 */

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaterGridRuntime : MonoBehaviour
{
    [Header("Grid (world units)")]
    [Tooltip("Total size in X (meters).")]
    public float sizeX = 200f;
    [Tooltip("Total size in Z (meters).")]
    public float sizeZ = 200f;
    [Tooltip("Number of quads across X. 80–140 is reasonable.")]
    public int  segX  = 100;
    [Tooltip("Number of quads across Z. 80–140 is reasonable.")]
    public int  segZ  = 100;

    [Header("Normals + perf")]
    [Tooltip("Recompute normals every N frames. 1 = every frame. 0 = never.")]
    public int updateNormalsEveryN = 2;

    [Tooltip("Optional Y offset if you don’t want to move the object.")]
    public float waterLevelOffsetY = 0f;

    MeshFilter mf;
    Mesh mesh;
    Vector3[] baseLocal;   // flat grid (local space, y=0)
    Vector3[] deformed;    // working verts
    Vector3[] normals;     // working normals
    Vector2[] uvs;
    int[] tris;
    int frame;
    WaveField waves;

    void Awake()
    {
        // Get/create components
        mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (!mr.sharedMaterial)
        {
            Debug.LogWarning("WaterGridRuntime: assign a visible material on the MeshRenderer.");
        }

        // Build flat grid
        BuildGrid();

        // Try find the global wave driver
        waves = WaveField.Instance;
        if (!waves)
            Debug.LogWarning("WaterGridRuntime: No WaveField found. Heights will be flat.");
    }

    void BuildGrid()
    {
        int vx = segX + 1;
        int vz = segZ + 1;
        int vCount = vx * vz;

        baseLocal = new Vector3[vCount];
        deformed  = new Vector3[vCount];
        normals   = new Vector3[vCount];
        uvs       = new Vector2[vCount];
        tris      = new int[segX * segZ * 6];

        float dx = sizeX / segX;
        float dz = sizeZ / segZ;
        float x0 = -sizeX * 0.5f;
        float z0 = -sizeZ * 0.5f;

        int vi = 0;
        for (int z = 0; z < vz; z++)
        {
            for (int x = 0; x < vx; x++, vi++)
            {
                float px = x0 + x * dx;
                float pz = z0 + z * dz;
                baseLocal[vi] = new Vector3(px, 0f, pz);
                deformed [vi] = baseLocal[vi];
                uvs[vi] = new Vector2((float)x / segX, (float)z / segZ);
                normals[vi] = Vector3.up;
            }
        }

        int ti = 0;
        for (int z = 0; z < segZ; z++)
        {
            for (int x = 0; x < segX; x++)
            {
                int i0 =  x      +  z      * vx;
                int i1 = (x + 1) +  z      * vx;
                int i2 =  x      + (z + 1) * vx;
                int i3 = (x + 1) + (z + 1) * vx;
                // tri 0
                tris[ti++] = i0; tris[ti++] = i3; tris[ti++] = i2;
                // tri 1
                tris[ti++] = i0; tris[ti++] = i1; tris[ti++] = i3;
            }
        }

        mesh = new Mesh { name = "WaterGridRuntimeMesh" };
        mesh.indexFormat = (vCount > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32
                                            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices  = deformed;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        mesh.normals   = normals;
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
    }

    void LateUpdate()
    {
        if (!mesh || baseLocal == null) return;

        // Deform heights
        for (int i = 0; i < baseLocal.Length; i++)
        {
            // Local → world
            Vector3 w = transform.TransformPoint(baseLocal[i]);
            // Force the base water level to WaveField level + optional offset
            if (waves != null)
            {
                w.y = waves.HeightAt(w) + waterLevelOffsetY;
            }
            else
            {
                // Flat fallback
                w.y = transform.position.y + waterLevelOffsetY;
            }
            // World → local
            deformed[i] = transform.InverseTransformPoint(w);
        }
        mesh.vertices = deformed;

        // Normals less often (finite difference via WaveField normals)
        frame++;
        if (updateNormalsEveryN > 0 && (frame % updateNormalsEveryN == 0))
        {
            if (waves != null)
            {
                for (int i = 0; i < deformed.Length; i++)
                {
                    Vector3 w = transform.TransformPoint(deformed[i]);
                    Vector3 nW = waves.NormalAt(w);
                    normals[i] = transform.InverseTransformDirection(nW).normalized;
                }
            }
            else
            {
                for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            }
            mesh.normals = normals;
        }

        mesh.RecalculateBounds(); // keep culling correct
    }
}
