using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 장비 공통 데이터. 모든 장비가 이 데이터를 보유한다.
    /// </summary>
    [System.Serializable]
    public class EquipmentCommonData
    {
        public string id;
        public string displayName;
        public string description;
        public Sprite icon;
        [HideInInspector]
        public EquipmentCategory category;  // 런타임 호환용 유지
        public string slotType;    // SlotType 참고
        public float weight;
        public int maxDurability; // 0 = 파괴 불가

        [Header("패시브 스탯 보너스")]
        public EquipmentStatModifier[] statModifiers = new EquipmentStatModifier[0];

        // [확장포인트] 패시브 버프 — 받는 게임의 버프 SO를 꽂는다.
        // 적용은 ExampleEquipmentController에서 자기 버프 타입으로 캐스팅해 처리.
        [Header("패시브 버프 (게임 버프 SO)")]
        public ScriptableObject[] passiveBuffs = new ScriptableObject[0];

        // [확장포인트] 액티브 스킬 — 받는 게임의 스킬/액션 SO를 꽂는다.
        [Header("액티브 스킬 (게임 스킬 SO)")]
        public ScriptableObject[] activeAbilities = new ScriptableObject[0];
    }
}
