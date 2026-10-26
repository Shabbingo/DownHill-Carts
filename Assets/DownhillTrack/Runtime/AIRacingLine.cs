using UnityEngine;

namespace DownhillCarts
{
    public sealed class AIRacingLine : MonoBehaviour
    {
        [SerializeField] private Vector3[] points;

        public int PointCount => points == null ? 0 : points.Length;

        public void Configure(Vector3[] worldPoints) => points = worldPoints;

        public Vector3 GetPoint(int index)
        {
            if (PointCount == 0)
                return transform.position;
            return points[Mathf.Clamp(index, 0, PointCount - 1)];
        }

        public Vector3 GetDirection(int index)
        {
            if (PointCount < 2)
                return transform.forward;
            int current = Mathf.Clamp(index, 0, PointCount - 2);
            return (points[current + 1] - points[current]).normalized;
        }

        public int FindClosestPoint(Vector3 position, int startIndex = 0)
        {
            int bestIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, PointCount - 1));
            float bestDistance = float.MaxValue;
            for (int i = bestIndex; i < PointCount; i++)
            {
                float distance = (points[i] - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
                else if (i > bestIndex + 18)
                {
                    break;
                }
            }
            return bestIndex;
        }

        private void OnDrawGizmosSelected()
        {
            if (points == null || points.Length < 2)
                return;
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.8f);
            for (int i = 0; i < points.Length - 1; i++)
                Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }
}
