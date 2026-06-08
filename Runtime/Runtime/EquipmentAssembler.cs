using System.Collections.Generic;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// Equipment 전용 조립 유틸리티. Stateless static.
    /// 범용 로직(IsCompatible, HasRequiredParts, CalculateTotalWeight)은 AssemblyAssembler로 이동됨.
    /// 여기는 EquipmentStatModifier 수집 등 Equipment 도메인 전용만 남는다.
    /// </summary>
    public static class EquipmentAssembler
    {
        /// <summary>
        /// 장착된 파츠들의 스탯 수정자를 수집한다.
        /// </summary>
        public static List<EquipmentStatModifier> CollectPartModifiers(PartData[] parts)
        {
            var modifiers = new List<EquipmentStatModifier>();
            if (parts == null) return modifiers;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                var c = parts[i].contribution;
                if (c == null || c.statModifiers == null) continue;

                for (int j = 0; j < c.statModifiers.Length; j++)
                    modifiers.Add(c.statModifiers[j]);
            }

            return modifiers;
        }
    }
}
