// WaveField.cs
// Small two-component chop for physics + visuals. Unchanged API, mild defaults.
using UnityEngine;

public class WaveField : MonoBehaviour
{
    [Header("Global")]
    [SerializeField] float waterLevelY = 0f;
    [SerializeField] float density = 1000f;

    [Header("Gerstner A")]
    [SerializeField] float ampA = 0.07f;
    [SerializeField] float wavelengthA = 12f;
    [SerializeField] float speedA = 1.0f;
    [SerializeField] Vector2 dirA = new Vector2(1, 0);

    [Header("Gerstner B")]
    [SerializeField] float ampB = 0.045f;
    [SerializeField] float wavelengthB = 8f;
    [SerializeField] float speedB = 0.85f;
    [SerializeField] Vector2 dirB = new Vector2(0.3f, 0.95f);

    public static WaveField Instance { get; private set; }
    public float Density => density;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        dirA.Normalize(); dirB.Normalize();
    }

    public float HeightAt(Vector3 worldPos)
    {
        float y = waterLevelY;
        y += Gerstner(worldPos, ampA, wavelengthA, speedA, dirA);
        y += Gerstner(worldPos, ampB, wavelengthB, speedB, dirB);
        return y;
    }

    public Vector3 SurfaceVelocityAt(Vector3 worldPos)
    {
        Vector2 v = Vector2.zero;
        v += Flow(worldPos, ampA, wavelengthA, speedA, dirA);
        v += Flow(worldPos, ampB, wavelengthB, speedB, dirB);
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
        float mag = A * w * 0.12f * Mathf.Cos(phase); // subtle current
        return dir * mag;
    }
}
