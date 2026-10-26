using UnityEngine;

namespace DownhillCarts
{
    [ExecuteAlways]
    public sealed class AIRacerGroup : MonoBehaviour
    {
        [SerializeField] private bool enableAIRacers = true;
        [SerializeField, Range(0, 12)] private int activeRacerCount = 5;

        public void Configure(bool enabled, int racerCount)
        {
            enableAIRacers = enabled;
            activeRacerCount = racerCount;
            ApplySelection();
        }

        private void Awake() => ApplySelection();

        private void OnValidate() => ApplySelection();

        private void ApplySelection()
        {
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(enableAIRacers && i < activeRacerCount);
        }
    }
}
