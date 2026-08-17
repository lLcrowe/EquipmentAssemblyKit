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
            CollectPartModifiersRecursive(parts, modifiers);
            return modifiers;
        }

        private static void CollectPartModifiersRecursive(
            PartData[] parts,
            List<EquipmentStatModifier> modifiers)
        {
            if (parts == null) return;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == null) continue;

                var contribution = part.contribution;
                if (contribution != null && contribution.statModifiers != null)
                {
                    for (int j = 0; j < contribution.statModifiers.Length; j++)
                        modifiers.Add(contribution.statModifiers[j]);
                }

                CollectPartModifiersRecursive(part.childParts, modifiers);
            }
        }
    }
}
