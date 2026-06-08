using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 재귀 파츠 SO. 장비(EquipmentInfo)의 슬롯에 장착되는 부품.
    /// childSlots를 통해 하위 파츠를 재귀적으로 보유할 수 있다.
    /// 장비/차량/로봇 등 모든 조립 시스템에서 공용으로 사용.
    /// </summary>
    [CreateAssetMenu(fileName = "New PartInfo", menuName = "lLcroweTool/Equipment/Part Info")]
    public class PartInfo : ScriptableObject
    {
        [Header("파츠 분류")]
        [PartTag]
        public string[] partTags = new string[0];

        [Header("기본 정보")]
        public float weight;
        public Sprite icon;

        [Header("하위 슬롯 (재귀)")]
        public PartSlot[] childSlots = new PartSlot[0];

        [Header("기여 데이터")]
        public PartContribution contribution;

        [Header("조립 상태 (에디터 하위 조립 결과)")]
        public SlotPartEntry[] assembledChildParts = new SlotPartEntry[0];
    }
}
