using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 장비 홀더 SO. 장비 정체성(공통 데이터)과 루트 슬롯을 보유한다.
    /// 파츠 기여 데이터는 PartInfo로 분리됨.
    ///
    /// [확장] 무기/방어구/컨테이너 같은 카테고리별 데이터는 partial로 덧붙인다.
    /// (예제: PartContribution.GameExtension.cs.txt 의 EquipmentInfo 섹션)
    /// </summary>
    [CreateAssetMenu(fileName = "New EquipmentInfo", menuName = "lLcroweTool/Equipment/Equipment Info")]
    public partial class EquipmentInfo : ScriptableObject
    {
        public EquipmentCommonData equipmentInfo = new EquipmentCommonData();

        [Header("루트 슬롯 (이 장비가 가진 슬롯)")]
        public PartSlot[] rootSlots = new PartSlot[0];

        [Header("조립 상태 (에디터 조립 결과)")]
        public SlotPartEntry[] assembledParts = new SlotPartEntry[0];

        /// <summary>
        /// 조립에 사용된 코어 파츠 참조
        /// </summary>
        public PartInfo assembledCore;
    }
}
