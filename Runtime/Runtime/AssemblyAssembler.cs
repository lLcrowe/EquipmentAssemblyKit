namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 범용 조립 유틸리티. Stateless static.
    /// 태그 호환성, 필수 파츠 체크, 무게 계산 등 Equipment/Vehicle/Robot 공용 로직.
    /// Equipment 전용(스탯 수정자 수집)은 EquipmentAssembler에 남아있다.
    /// </summary>
    public static class AssemblyAssembler
    {
        /// <summary>
        /// Tag 호환성 체크. 슬롯의 acceptedTags와 파츠의 partTags가 하나라도 겹치면 호환.
        /// </summary>
        public static bool IsCompatible(in PartSlot slot, in PartInfo part)
        {
            if (slot.acceptedTags == null || slot.acceptedTags.Length == 0) return true;
            if (part == null || part.partTags == null || part.partTags.Length == 0) return false;

            for (int i = 0; i < slot.acceptedTags.Length; i++)
            {
                for (int j = 0; j < part.partTags.Length; j++)
                {
                    if (slot.acceptedTags[i] == part.partTags[j])
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 필수 파츠가 모두 채워졌는지 체크한다.
        /// </summary>
        public static bool HasRequiredParts(PartSlot[] slots)
        {
            if (slots == null) return true;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].isRequired && slots[i].IsEmpty)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 총 무게 계산 (베이스 무게 + 파츠 무게 재귀 합산)
        /// </summary>
        public static float CalculateTotalWeight(float baseWeight, PartData[] parts)
        {
            float weight = baseWeight;

            if (parts == null) return weight;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                weight += CalculatePartWeightRecursive(parts[i]);
            }

            return weight > 0f ? weight : 0f;
        }

        private static float CalculatePartWeightRecursive(PartData part)
        {
            if (part == null) return 0f;

            float weight = part.source.weight;

            if (part.childParts != null)
            {
                for (int i = 0; i < part.childParts.Length; i++)
                {
                    weight += CalculatePartWeightRecursive(part.childParts[i]);
                }
            }

            return weight;
        }
    }
}
