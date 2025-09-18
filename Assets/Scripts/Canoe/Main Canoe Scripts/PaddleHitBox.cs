using UnityEngine;

/* PaddleHitbox
 * Trigger collider on the paddle blade.
 * Notifies other canoes’ CanoeBalance when overlapping.
 * Hit strength scales with the owner’s paddle tip speed.
 */
[RequireComponent(typeof(Collider))]
public class PaddleHitbox : MonoBehaviour
{
    [Header("Ownership")]
    [SerializeField] Transform ownerRoot;

    [Header("Tuning")]
    [SerializeField] float minInterval = 0.25f;          // per-target cooldown
    [SerializeField, Range(0f,1f)] float baseStrength01 = 0.6f;
    [SerializeField] float minImpactSpeed = 0.6f;        // m/s for noticeable hit
    [SerializeField] float maxImpactSpeed = 3.5f;        // m/s for full hit

    CanoePaddleController ownerCtrl;

    readonly System.Collections.Generic.Dictionary<CanoeBalance,float> lastHit = new();

    void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    public void BindOwner(CanoePaddleController ctrl)
    {
        ownerCtrl = ctrl;
        ownerRoot = ctrl ? ctrl.transform : ownerRoot;
    }

    public void SetOwner(Transform root) => ownerRoot = root;

    void OnTriggerEnter(Collider other) { TryHit(other); }
    void OnTriggerStay (Collider other) { TryHit(other); }

    void TryHit(Collider other)
    {
        if (!ownerRoot) return;
        if (other.transform.IsChildOf(ownerRoot)) return; // ignore self

        var victim = other.GetComponentInParent<CanoeBalance>();
        if (!victim) return;

        float now = Time.time;
        if (lastHit.TryGetValue(victim, out float t) && now - t < minInterval) return;

        // Direction from attacker to victim
        Vector3 dir = (victim.transform.position - ownerRoot.position).normalized;

        // Scale by tip speed (and slightly higher when blade is wet)
        float tipSpeed = ownerCtrl ? ownerCtrl.LastTipSpeed : 0f;
        float speedFactor = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, tipSpeed);
        float wetBonus = (ownerCtrl != null && ownerCtrl.BladeWet) ? 1.15f : 1f;
        float strength = Mathf.Clamp01(baseStrength01 * speedFactor * wetBonus);

        victim.ApplyPaddleHit(dir, strength);
        lastHit[victim] = now;
    }
}
