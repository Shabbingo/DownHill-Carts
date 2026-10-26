using UnityEngine;

namespace DownhillCarts
{
    public sealed class SimpleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 localOffset = new(0f, 3.2f, -6.5f);
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.15f;
        [SerializeField, Min(0f)] private float lookAhead = 3f;

        private Vector3 velocity;

        public void SetTarget(Transform newTarget) => target = newTarget;

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.TransformPoint(localOffset);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, positionSmoothTime);
            Vector3 lookPoint = target.position + target.forward * lookAhead;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }
    }
}
