using UnityEngine;

/*
 * BladeNoPushCollider
 *  - Ensures the blade collider does not impart forces to the canoe via physics contacts.
 *  - Use this if you must keep a collider for VFX/triggers but want zero rigidbody impulses.
 *  - Marks all blade colliders as triggers at runtime. Hydrodynamic force is handled by PaddleSystem.
 */

[RequireComponent(typeof(Collider))]
public class BladeNoPushCollider : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // no impulses on contact
    }
}
