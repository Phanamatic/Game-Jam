using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WaterSurfaceDeformer : MonoBehaviour
{
    public WaveField waves;

    [Header("Performance")]
    [Tooltip("Vertex grid bigger than ~70×70 can be heavy. Keep low for testing.")]
    [SerializeField] int updateNormalsEveryN = 2; // 0 = never, 1 = every frame, 2 = every 2 frames…

    MeshFilter mf;
    Mesh deformMesh;
    Vector3[] baseVerts;
    Vector3[] verts;
    Vector3[] normals;
    int frame;

    void Awake()
    {
        waves = waves ? waves : WaveField.Instance;
        mf = GetComponent<MeshFilter>();

        // Copy once so we don’t mutate shared mesh
        deformMesh = Instantiate(mf.sharedMesh);
        deformMesh.name = mf.sharedMesh.name + " (DeformedCopy)";
        mf.mesh = deformMesh;

        baseVerts = deformMesh.vertices;
        verts     = new Vector3[baseVerts.Length];
        normals   = new Vector3[baseVerts.Length];
    }

    void LateUpdate()
    {
        if (!waves) return;

        // Deform Y
        for (int i = 0; i < baseVerts.Length; i++)
        {
            Vector3 vLocal = baseVerts[i];
            Vector3 vWorld = transform.TransformPoint(vLocal);
            vWorld.y = waves.HeightAt(vWorld);
            verts[i] = transform.InverseTransformPoint(vWorld);
        }
        deformMesh.vertices = verts;

        // Recompute normals less often
        frame++;
        if (updateNormalsEveryN > 0 && (frame % updateNormalsEveryN == 0))
        {
            for (int i = 0; i < baseVerts.Length; i++)
            {
                Vector3 vWorld = transform.TransformPoint(verts[i]);
                Vector3 nWorld = waves.NormalAt(vWorld);
                normals[i] = transform.InverseTransformDirection(nWorld).normalized;
            }
            deformMesh.normals = normals;
        }

        deformMesh.RecalculateBounds();
    }
}
