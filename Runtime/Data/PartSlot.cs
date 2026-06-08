using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 파츠 슬롯 정의. 장비가 보유한 슬롯 하나를 나타낸다.
    /// acceptedTags와 파츠의 partTags가 하나라도 겹치면 장착 가능.
    /// </summary>
    [System.Serializable]
    public struct PartSlot
    {
        public string slotId;               // "HEAD", "BARREL", "PLATE_FRONT"
        [PartTag]
        public string[] acceptedTags;       // ["blunt", "explosive"]
        public bool isRequired;             // true = 없으면 장비 기능 불완전

        [Header("비주얼")]
        public string attachPointId;        // 부착점 이름 ("MUZZLE", "SCOPE_RAIL")
        public Vector3 localPosition;       // 슬롯 위치 (부모 기준)
        public Vector3 localRotation;       // 슬롯 회전 (오일러각, 부모 기준)

        [Header("애니메이션 앵커")]
        [Tooltip("이 슬롯의 장착 파츠를 애니메이션 IK 타겟으로 사용할 때의 역할.\n비어있으면 IK 대상 아님")]
        [PartTag]
        public string animAnchor;           // "main_hand", "sub_hand" 등

        [Header("연결 대상")]
        [PartTag]
        public string connectToTag;         // 연결 대상 파츠 태그 (벨트, 슬링 등)
        public PartConnectorVisual connectVisualPrefab; // 연결 비주얼 프리팹 (베지어 곡선 + 렌더러)

        [System.NonSerialized]
        public PartData equipped;           // 런타임 장착 상태

        public bool IsEmpty => equipped == null;
    }
}
