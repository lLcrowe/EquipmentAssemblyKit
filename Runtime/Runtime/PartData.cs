using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 런타임 파츠 인스턴스. 장비에 장착된 개별 파츠의 상태를 추적한다.
    /// </summary>
    public class PartData
    {
        public PartInfo source;
        public PartContribution contribution;
        public int slotIndex;

        // 비주얼
        public GameObject visualInstance;       // 런타임 생성된 비주얼 인스턴스
        public int currentVariantIndex = -1;    // 현재 선택된 변형 (-1 = 기본 visualPrefab 사용)

        // 자식 파츠 (트리 구조)
        // source.childSlots[i]에 대응하는 장착 파츠. null이면 하위 슬롯 없음.
        public PartData[] childParts;

        public PartData(PartInfo partItem, int slot)
        {
            source = partItem;
            contribution = partItem.contribution;
            slotIndex = slot;

            // 하위 슬롯 초기화
            int childSlotCount = partItem.childSlots != null ? partItem.childSlots.Length : 0;
            childParts = childSlotCount > 0 ? new PartData[childSlotCount] : null;
        }
    }
}
