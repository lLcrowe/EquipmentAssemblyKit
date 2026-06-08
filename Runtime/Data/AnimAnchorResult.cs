using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 애니메이션 앵커 조회 결과.
    /// 슬롯의 animAnchor 매칭 시 장착된 파츠의 Transform 정보를 담는다.
    /// </summary>
    public struct AnimAnchorResult
    {
        public string anchorTag;        // 매칭된 animAnchor ("main_hand", "sub_hand")
        public Transform transform;     // 장착 파츠의 visualInstance.transform
        public PartData partData;       // 해당 파츠 데이터 (contribution 접근용)
        public int slotIndex;           // 슬롯 인덱스 (루트 기준, 자식이면 childSlot 인덱스)
    }
}
