using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 장비의 비주얼 부착점을 관리한다.
    /// 장비 프리팹에 부착하여 파츠가 달릴 위치(Transform)를 정의한다.
    /// </summary>
    public class EquipmentVisualHolder : MonoBehaviour
    {
        [System.Serializable]
        public struct AttachPoint
        {
            public string pointId;      // "MUZZLE", "SCOPE_RAIL", "GRIP", "STOCK"
            public Transform point;     // 실제 위치
        }

        [SerializeField] private AttachPoint[] attachPoints = new AttachPoint[0];

        public AttachPoint[] AttachPoints => attachPoints;

        /// <summary>
        /// pointId로 Transform 검색
        /// </summary>
        public Transform GetAttachPoint(string pointId)
        {
            if (string.IsNullOrEmpty(pointId)) return null;
            if (attachPoints == null) return null;

            for (int i = 0; i < attachPoints.Length; i++)
            {
                if (attachPoints[i].pointId == pointId)
                    return attachPoints[i].point;
            }

            return null;
        }

        /// <summary>
        /// pointId 인덱스 검색
        /// </summary>
        public int FindAttachPointIndex(string pointId)
        {
            if (string.IsNullOrEmpty(pointId)) return -1;
            if (attachPoints == null) return -1;

            for (int i = 0; i < attachPoints.Length; i++)
            {
                if (attachPoints[i].pointId == pointId)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 파츠 비주얼 부착. 부착점 아래에 프리팹을 Instantiate한다.
        /// </summary>
        public GameObject AttachVisual(string attachPointId, GameObject prefab)
        {
            if (prefab == null) return null;

            var point = GetAttachPoint(attachPointId);
            if (point == null)
            {
                Debug.LogWarning($"[EquipmentVisualHolder] 부착점 '{attachPointId}'을 찾을 수 없습니다.");
                return null;
            }

            var instance = Instantiate(prefab, point);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        /// <summary>
        /// 파츠 비주얼 제거. 인스턴스를 파괴한다.
        /// </summary>
        public void DetachVisual(GameObject instance)
        {
            if (instance == null) return;

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// 공식 기반 비주얼 부착.
        /// PartSlot.localPosition/localRotation + PartContribution.visualOffset/visualRotationOffset을
        /// 사용하여 파츠를 정확한 위치에 배치한다.
        /// attachPointId가 있으면 해당 부착점을 부모로, 없으면 자신의 Transform을 부모로 사용.
        /// </summary>
        public GameObject AttachVisualWithFormula(
            string attachPointId, GameObject prefab,
            in PartSlot slot, PartContribution contribution)
        {
            if (prefab == null) return null;

            // 부모 Transform 결정
            Transform parent = transform;
            if (!string.IsNullOrEmpty(attachPointId))
            {
                var found = GetAttachPoint(attachPointId);
                if (found != null) parent = found;
            }

            EquipmentVisualAssembler.ComputePartTransform(
                parent.position, parent.rotation,
                slot, contribution,
                out var finalPos, out var finalRot);

            var instance = Instantiate(prefab, parent);
            instance.transform.position = finalPos;
            instance.transform.rotation = finalRot;
            return instance;
        }

        /// <summary>
        /// 변형 교체. 기존 인스턴스를 파괴하고 새 프리팹을 Instantiate한다.
        /// </summary>
        public GameObject SwapVisual(string attachPointId, GameObject oldInstance, GameObject newPrefab)
        {
            DetachVisual(oldInstance);
            return AttachVisual(attachPointId, newPrefab);
        }

        /// <summary>
        /// 모든 자식 비주얼 제거
        /// </summary>
        public void ClearAll()
        {
            if (attachPoints == null) return;

            for (int i = 0; i < attachPoints.Length; i++)
            {
                var point = attachPoints[i].point;
                if (point == null) continue;

                // 역순으로 자식 파괴
                for (int c = point.childCount - 1; c >= 0; c--)
                {
                    var child = point.GetChild(c).gameObject;
                    if (Application.isPlaying)
                    {
                        Destroy(child);
                    }
                    else
                    {
                        DestroyImmediate(child);
                    }
                }
            }
        }
    }
}
