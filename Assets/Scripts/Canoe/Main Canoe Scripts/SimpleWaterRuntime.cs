using UnityEngine;

/*
 * SimpleWaterRuntime
 * Runtime water grid + Gerstner waves.
 * Adds:
 *  - invertDisplacement: flip wave vertical motion
 *  - flipFaces: reverse triangle winding so the visible side faces up
 */
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SimpleWaterRuntime : MonoBehaviour
{
    [Header("Level + Physics")]
    public float waterLevelY = 0f;
    public float density = 1000f;

    [Header("Grid (world units)")]
    public float sizeX = 200f;
    public float sizeZ = 200f;
    public int   segX  = 100;
    public int   segZ  = 100;

    [Header("Waves A")]
    public float ampA = 0.07f;
    public float wavelengthA = 12f;
    public float speedA = 1.0f;
    public Vector2 dirA = new(1, 0);

    [Header("Waves B")]
    public float ampB = 0.045f;
    public float wavelengthB = 8f;
    public float speedB = 0.85f;
    public Vector2 dirB = new(0.3f, 0.95f);

    [Header("Options")]
    [Tooltip("Flip vertical displacement of waves.")]
    public bool invertDisplacement = false;
    [Tooltip("Reverse triangle winding so the top renders upward.")]
    public bool flipFaces = true;
    [Tooltip("Recompute normals every N frames. 1 = every frame. 0 = never.")]
    public int updateNormalsEveryN = 2;

    public static SimpleWaterRuntime Instance { get; private set; }

    MeshFilter mf;
    Mesh mesh;
    Vector3[] baseLocal;
    Vector3[] deformed;
    Vector3[] normals;
    Vector2[] uvs;
    int[] tris;
    int frame;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        dirA.Normalize(); dirB.Normalize();

        mf = GetComponent<MeshFilter>();
        if (!TryGetComponent<MeshRenderer>(out _)) gameObject.AddComponent<MeshRenderer>();

        BuildGrid();
    }

    void BuildGrid()
    {
        int vx = Mathf.Max(2, segX + 1);
        int vz = Mathf.Max(2, segZ + 1);
        int vCount = vx * vz;

        baseLocal = new Vector3[vCount];
        deformed  = new Vector3[vCount];
        normals   = new Vector3[vCount];
        uvs       = new Vector2[vCount];
        tris      = new int[segX * segZ * 6];

        float dx = sizeX / Mathf.Max(1, segX);
        float dz = sizeZ / Mathf.Max(1, segZ);
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
                uvs[vi]       = new Vector2((float)x / segX, (float)z / segZ);
                normals[vi]   = Vector3.up;
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

                // Default winding makes normals face DOWN on some imports.
                // We'll flip later if flipFaces==true.
                tris[ti++] = i0; tris[ti++] = i3; tris[ti++] = i2;
                tris[ti++] = i0; tris[ti++] = i1; tris[ti++] = i3;
            }
        }

        if (flipFaces)
        {
            // Reverse winding: swap the last two indices of each triangle
            for (int i = 0; i < tris.Length; i += 3)
            {
                int tmp = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = tmp;
            }
            // Also invert initial normals so lighting matches
            for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
        }

        mesh = new Mesh { name = "SimpleWaterRuntimeMesh" };
        mesh.indexFormat = (vCount > 65535)
            ? UnityEngine.Rendering.IndexFormat.UInt32
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
        if (!mesh) return;

        for (int i = 0; i < baseLocal.Length; i++)
        {
            Vector3 w = transform.TransformPoint(baseLocal[i]);
            w.y = HeightAt(w);
            deformed[i] = transform.InverseTransformPoint(w);
        }
        mesh.vertices = deformed;

        frame++;
        if (updateNormalsEveryN > 0 && (frame % updateNormalsEveryN == 0))
        {
            for (int i = 0; i < deformed.Length; i++)
            {
                Vector3 w = transform.TransformPoint(deformed[i]);
                Vector3 nW = NormalAt(w);
                if (flipFaces) nW = -nW; // keep lighting consistent with flipped faces
                normals[i] = transform.InverseTransformDirection(nW).normalized;
            }
            mesh.normals = normals;
        }

        mesh.RecalculateBounds();
    }

    // ── Wave API ──
    public float HeightAt(Vector3 worldPos)
    {
        float s = invertDisplacement ? -1f : 1f;
        float y = waterLevelY;
        y += s * Gerstner(worldPos, ampA, wavelengthA, speedA, dirA);
        y += s * Gerstner(worldPos, ampB, wavelengthB, speedB, dirB);
        return y;
    }

    public Vector3 SurfaceVelocityAt(Vector3 worldPos)
    {
        float s = invertDisplacement ? -1f : 1f;
        Vector2 v = Vector2.zero;
        v += s * Flow(worldPos, ampA, wavelengthA, speedA, dirA);
        v += s * Flow(worldPos, ampB, wavelengthB, speedB, dirB);
        return new Vector3(v.x, 0f, v.y);
    }

    public Vector3 NormalAt(Vector3 worldPos)
    {
        float eps = 0.25f;
        float hL = HeightAt(worldPos + Vector3.left   * eps);
        float hR = HeightAt(worldPos + Vector3.right  * eps);
        float hB = HeightAt(worldPos + Vector3.back   * eps);
        float hF = HeightAt(worldPos + Vector3.forward* eps);
        Vector3 n = new Vector3(hL - hR, 2f * eps, hB - hF);
        return n.normalized;
    }

    float Gerstner(Vector3 p, float A, float L, float S, Vector2 dir)
    {
        float k = 2f * Mathf.PI / Mathf.Max(0.001f, L);
        float w = Mathf.Sqrt(9.81f * k);
        float phase = k * (dir.x * p.x + dir.y * p.z) - (w * Time.time * S);
        return A * Mathf.Sin(phase);
    }

    Vector2 Flow(Vector3 p, float A, float L, float S, Vector2 dir)
    {
        float k = 2f * Mathf.PI / Mathf.Max(0.001f, L);
        float w = Mathf.Sqrt(9.81f * k);
        float phase = k * (dir.x * p.x + dir.y * p.z) - (w * Time.time * S);
        float mag = A * w * 0.12f * Mathf.Cos(phase);
        return dir * mag;
    }
}
