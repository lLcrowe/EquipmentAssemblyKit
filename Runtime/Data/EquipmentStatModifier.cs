namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 장비 스탯 수정자. 조립 결과로 합산되어 게임 스탯에 반영된다.
    /// statType은 게임별 스탯 ID(string) — 받는 게임의 스탯 시스템에 매핑한다.
    /// 실제 적용은 컴포지션으로 처리 (ExampleEquipmentController 참조).
    /// </summary>
    [System.Serializable]
    public struct EquipmentStatModifier
    {
        public string statType;          // 게임별 스탯 ID (예: "STAT_ATK", "STAT_DEF")
        public ModifierType modifierType;
        public float value;
    }
}
