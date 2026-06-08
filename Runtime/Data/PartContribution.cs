using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 파츠 기여 데이터 (코어). 파츠(PartInfo)가 부모 장비에 기여하는 내용.
    /// bool 플래그가 true인 섹션만 합산에 참여한다. partTags는 PartInfo 루트로 승격됨.
    ///
    /// [확장] 대미지/투사체/조준 등 게임별 전투 기여는 partial로 덧붙인다.
    /// 상속·인터페이스 없이 같은 클래스에 필드를 추가하는 컴포지션 방식.
    /// 예제: PartContribution.GameExtension.cs.txt
    /// </summary>
    [System.Serializable]
    public partial class PartContribution
    {
        [Header("스탯")]
        public EquipmentStatModifier[] statModifiers = new EquipmentStatModifier[0];

        // [확장포인트] 파츠 버프 — 받는 게임의 버프 SO를 꽂는다.
        [Header("버프 (게임 버프 SO)")]
        public ScriptableObject[] partBuffs = new ScriptableObject[0];

        [Header("방어 기여")]
        public bool hasDefense;
        public int defenseValue;
        public int hardness;

        [Header("컨테이너 기여")]
        public bool hasContainer;
        public int containerSlotCount;
        public float containerMaxWeight;
        public EquipmentCategory[] allowedCategories = new EquipmentCategory[0];

        [Header("잼")]
        public int jamProbability;

        [Header("비주얼")]
        public Renderer visualPrefab;                        // 파츠 외형 프리팹 (단일)
        public Renderer[] variants = new Renderer[0];        // 외형 변형 배열 (복수 선택 가능)
        public Vector3 visualOffset;                         // 파츠 비주얼 위치 보정 (장착 시 오프셋)
        public Vector3 visualRotationOffset;                 // 파츠 비주얼 회전 보정 (오일러각)

        [Header("사운드")]
        public AudioClip[] sounds = new AudioClip[0];        // 범용 랜덤 재생용 (발사음 등)
        public AudioClip[] equipSounds = new AudioClip[0];   // 장착 시 랜덤 재생
        public AudioClip[] unequipSounds = new AudioClip[0]; // 해제 시 랜덤 재생
    }
}
