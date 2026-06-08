using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 파츠 비주얼. SpriteRenderer를 사용하여 파츠 외형을 표시한다.
    /// </summary>
    public class PartVisual : MonoBehaviour
    {
        [Header("비주얼")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("연결")]
        [SerializeField] private Transform[] connectPoints;

        private PartData partData;

        public PartData CurrentPartData => partData;
        public Transform[] ConnectPoints => connectPoints;

        /// <summary>
        /// 파츠 데이터 주입 및 비주얼 초기화
        /// </summary>
        public void Init(PartData data)
        {
            partData = data;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null && data.source != null && data.source.icon != null)
            {
                spriteRenderer.sprite = data.source.icon;
            }
        }

        /// <summary>
        /// 특정 연결 포인트 위치 반환
        /// </summary>
        public Transform GetConnectPoint(int index)
        {
            if (connectPoints == null) return null;
            if (index < 0 || index >= connectPoints.Length) return null;
            return connectPoints[index];
        }

        /// <summary>
        /// 비주얼 활성화/비활성화
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
