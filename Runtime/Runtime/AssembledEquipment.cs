using System.Collections.Generic;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 조립된 장비 베이스 클래스. 태그 기반 파츠 시스템의 핵심 데이터.
    /// source(SO 원본) + parts(장착된 파츠)를 보유하고 스탯을 합산한다.
    /// 게임별 확장은 이 클래스를 상속하거나 Recompute()를 override한다.
    /// </summary>
    public class AssembledEquipment
    {
        public EquipmentInfo source;
        public EquipmentCommonData commonData;
        public int currentDurability;
        public PartData[] parts;

        public AssembledEquipment() { }

        public AssembledEquipment(EquipmentInfo asset)
        {
            source = asset;
            commonData = asset.equipmentInfo;
            currentDurability = commonData.maxDurability;

            int slotCount = asset.rootSlots != null ? asset.rootSlots.Length : 0;
            parts = new PartData[slotCount];
        }

        /// <summary>
        /// 파츠 변경 시 호출. 하위 클래스에서 도메인 데이터를 재계산한다.
        /// </summary>
        public virtual void Recompute() { }

        /// <summary>
        /// 기본 스탯 수정자 + 파츠 스탯 수정자 합산
        /// </summary>
        public List<EquipmentStatModifier> GetTotalStatModifiers()
        {
            var result = new List<EquipmentStatModifier>();

            if (commonData.statModifiers != null)
                result.AddRange(commonData.statModifiers);

            var partModifiers = EquipmentAssembler.CollectPartModifiers(parts);
            result.AddRange(partModifiers);

            return result;
        }

        /// <summary>
        /// 장비의 액티브 스킬 목록 반환 (게임 스킬 SO).
        /// </summary>
        public ScriptableObject[] GetActiveAbilities()
        {
            return commonData.activeAbilities;
        }
    }
}
