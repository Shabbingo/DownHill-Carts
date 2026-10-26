using System.Collections.Generic;
using UnityEngine;

namespace DownhillCarts
{
    public sealed class CartRiderRagdoll : MonoBehaviour
    {
        [SerializeField] private Collider[] riderColliders;
        [SerializeField, Min(1f)] private float ragdollMass = 23f;
        [SerializeField, Min(0f)] private float impactKick = 2.5f;
        [SerializeField, Min(0f)] private float tumbleSpeed = 3f;

        private Collider[] cartColliders;
        private bool active;

        public void Configure(Collider[] colliders)
        {
            riderColliders = colliders;
            SetRiderCollisions(false);
        }

        private void Awake()
        {
            RemoveLegacyChildPhysics();

            if (riderColliders == null || riderColliders.Length == 0)
                riderColliders = GetComponentsInChildren<Collider>(true);

            Transform cartRoot = transform.root;
            Collider[] allColliders = cartRoot.GetComponentsInChildren<Collider>(true);
            List<Collider> cartOnly = new();
            foreach (Collider candidate in allColliders)
            {
                if (candidate != null && !candidate.transform.IsChildOf(transform))
                    cartOnly.Add(candidate);
            }
            cartColliders = cartOnly.ToArray();
            SetRiderCollisions(false);
        }

        public void Activate(Vector3 inheritedVelocity, Vector3 impactPoint)
        {
            if (active)
                return;

            active = true;
            transform.SetParent(null, true);

            Rigidbody ragdollBody = gameObject.AddComponent<Rigidbody>();
            ragdollBody.mass = ragdollMass;
            ragdollBody.interpolation = RigidbodyInterpolation.Interpolate;
            ragdollBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ragdollBody.linearVelocity = inheritedVelocity;
            ragdollBody.angularVelocity = transform.right * tumbleSpeed;

            IgnoreCartCollisions();
            SetRiderCollisions(true);

            Vector3 away = (ragdollBody.worldCenterOfMass - impactPoint).normalized;
            ragdollBody.AddForce(away * impactKick, ForceMode.Impulse);
        }

        private void SetRiderCollisions(bool enabled)
        {
            if (riderColliders == null)
                return;

            foreach (Collider riderCollider in riderColliders)
            {
                if (riderCollider != null)
                    riderCollider.enabled = enabled;
            }
        }

        private void IgnoreCartCollisions()
        {
            if (riderColliders == null || cartColliders == null)
                return;

            foreach (Collider riderCollider in riderColliders)
            {
                if (riderCollider == null)
                    continue;
                foreach (Collider cartCollider in cartColliders)
                {
                    if (cartCollider != null)
                        Physics.IgnoreCollision(riderCollider, cartCollider, true);
                }
            }
        }

        private void RemoveLegacyChildPhysics()
        {
            foreach (Joint joint in GetComponentsInChildren<Joint>(true))
            {
                Destroy(joint);
            }

            foreach (Rigidbody childBody in GetComponentsInChildren<Rigidbody>(true))
            {
                if (childBody.gameObject == gameObject)
                    continue;
                childBody.detectCollisions = false;
                childBody.isKinematic = true;
                Destroy(childBody);
            }
        }
    }
}
