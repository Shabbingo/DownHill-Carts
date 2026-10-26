using UnityEngine;

namespace DownhillCarts
{
    public sealed class TrackCheckpoint : MonoBehaviour
    {
        [SerializeField, Min(1)] private int checkpointOrder = 1;
        [SerializeField, Min(0f)] private float respawnDistancePastGate = 4f;
        [SerializeField, Min(0f)] private float respawnHeight = 0.8f;

        public void Configure(int order) => checkpointOrder = order;

        private void OnTriggerEnter(Collider other)
        {
            SoapboxCartController cart = other.GetComponentInParent<SoapboxCartController>();
            if (cart == null || !cart.IsPlayerControlled)
                return;

            TimeTrialManager manager = FindAnyObjectByType<TimeTrialManager>();
            if (manager == null)
                return;

            Vector3 respawnPosition = transform.position
                + transform.forward * respawnDistancePastGate
                + transform.up * respawnHeight;
            manager.RegisterCheckpoint(cart, checkpointOrder, respawnPosition, transform.rotation);
        }
    }
}
