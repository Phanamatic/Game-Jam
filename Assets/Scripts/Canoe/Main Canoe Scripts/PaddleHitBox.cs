using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PaddleHitbox : MonoBehaviour
{
    [Header("Ownership")]
    [SerializeField] Transform ownerRoot;

    [Header("Tuning")]
    [SerializeField] float minInterval = 0.25f;
    [SerializeField, Range(0f,1f)] float baseStrength01 = 0.6f;
    [SerializeField] float minImpactSpeed = 0.6f;
    [SerializeField] float maxImpactSpeed = 3.5f;

    CanoePaddleController ownerCtrl;

    // Key by victim's BalanceMinigame now
    readonly System.Collections.Generic.Dictionary<BalanceMinigame,float> lastHit = new();

    void Reset(){ var c = GetComponent<Collider>(); c.isTrigger = true; }

    public void BindOwner(CanoePaddleController ctrl){ ownerCtrl = ctrl; ownerRoot = ctrl ? ctrl.transform : ownerRoot; }
    public void SetOwner(Transform root) => ownerRoot = root;

    void OnTriggerEnter(Collider other){ TryHit(other); }
    void OnTriggerStay (Collider other){ TryHit(other); }

    void TryHit(Collider other)
    {
        if (!ownerRoot) return;
        if (other.transform.IsChildOf(ownerRoot)) return;

        var victimUI = other.GetComponentInParent<BalanceMinigame>();
        if (!victimUI) return;

        float now = Time.time;
        if (lastHit.TryGetValue(victimUI, out float t) && now - t < minInterval) return;

        float tipSpeed = ownerCtrl ? ownerCtrl.LastTipSpeed : 0f;
        float speedFactor = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, tipSpeed);
        float wetBonus = (ownerCtrl != null && ownerCtrl.BladeWet) ? 1.15f : 1f;
        float strength = Mathf.Clamp01(baseStrength01 * speedFactor * wetBonus);
        if (strength <= 0f) return;

        // Side sign: + right side of victim, - left side
        Vector3 dirWorld = (victimUI.transform.position - ownerRoot.position).normalized;
        float sideSign = Mathf.Sign(Vector3.Dot(victimUI.transform.right, dirWorld));

        victimUI.ApplyHitDisruption(sideSign, strength);
        lastHit[victimUI] = now;
    }
}
