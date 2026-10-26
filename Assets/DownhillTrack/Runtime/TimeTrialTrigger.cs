using UnityEngine;

namespace DownhillCarts
{
    public sealed class TimeTrialTrigger : MonoBehaviour
    {
        public enum TriggerType
        {
            Start,
            Finish
        }

        [SerializeField] private TriggerType triggerType;

        public void Configure(TriggerType type) => triggerType = type;

        private void OnTriggerEnter(Collider other)
        {
            SoapboxCartController cart = other.GetComponentInParent<SoapboxCartController>();
            if (cart == null || !cart.IsPlayerControlled)
                return;

            TimeTrialManager manager = FindAnyObjectByType<TimeTrialManager>();
            if (manager == null)
                return;

            if (triggerType == TriggerType.Start)
                manager.BeginRace(cart);
            else
                manager.FinishRace(cart);
        }
    }
}
