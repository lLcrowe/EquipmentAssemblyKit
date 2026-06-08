using System;
using System.Collections.Generic;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// [예제] 장비 컨트롤러 — AssemblyController 골격에 게임 스탯/버프 적용을 연결하는 법.
    ///
    /// EquipmentAssemblyKit는 조립 메커니즘(슬롯/태그/비주얼/스탯 합산)만 제공한다.
    /// "합산된 스탯을 실제 게임 스탯에 어떻게 반영하는가"는 게임마다 다르므로
    /// 이 예제처럼 컴포지션으로 연결한다.
    ///
    /// 연결 방법:
    ///  (A) Action 콜백 주입 — ApplyStat / RemoveStat / ApplyBuff / RemoveBuff 델리게이트를
    ///      자기 게임의 스탯·버프 시스템으로 등록한다. (상속·인터페이스 없이 컴포지션)
    ///  (B) 이 클래스를 복제해 게임 의존부(주석 처리된 lLcrowe 원본)를 직접 채운다.
    /// </summary>
    [DefaultExecutionOrder(1)]
    public class ExampleEquipmentController : AssemblyController<AssembledEquipment>
    {
        // ── (A) 컴포지션 콜백 — 게임 스탯/버프 시스템을 외부에서 주입 ──
        // statType(string) + modifierType + value 를 받아 자기 게임 스탯에 반영한다.
        public Action<string, ModifierType, float> ApplyStat;     // 스탯 적용
        public Action<string, ModifierType, float> RemoveStat;    // 스탯 역산
        public Action<ScriptableObject> ApplyBuff;                // 버프 등록 (게임 버프 SO)
        public Action<ScriptableObject> RemoveBuff;               // 버프 해제

        public event Action OnStatsChanged;

        private readonly Dictionary<AssembledEquipment, List<EquipmentStatModifier>> appliedModifiers
            = new Dictionary<AssembledEquipment, List<EquipmentStatModifier>>();
        private readonly Dictionary<AssembledEquipment, List<ScriptableObject>> appliedBuffs
            = new Dictionary<AssembledEquipment, List<ScriptableObject>>();

        // ── AssemblyController 데이터 접근 구현 ──
        protected override PartSlot[] GetRootSlots(AssembledEquipment assembled) => assembled.source.rootSlots;
        protected override PartData[] GetParts(AssembledEquipment assembled) => assembled.parts;

        // ── AssemblyController 도메인 훅 ──
        protected override void OnPartEquipped(AssembledEquipment assembled, PartData partData)
        {
            ApplyPartModifiers(partData);
            ApplyPartBuffs(partData);
        }

        protected override void OnPartUnequipped(AssembledEquipment assembled, PartData partData)
        {
            RemovePartModifiers(partData);
            RemovePartBuffs(partData);
        }

        protected override void OnAssemblyChanged(AssembledEquipment assembled)
        {
            RefreshEquipmentStats(assembled);
        }

        /// <summary>
        /// 장비의 모든 파츠 비주얼을 재구축한다. (씬 로드, 장비 교체, UI 미리보기)
        /// </summary>
        public override void RebuildAllVisuals(AssembledEquipment assembled, EquipmentVisualHolder visualHolder)
        {
            if (assembled == null || visualHolder == null) return;
            visualHolder.ClearAll();
            EquipmentVisualAssembler.RebuildAllVisuals(assembled, visualHolder.transform);
        }

        // ── 장비 등록/해제 ──

        public void RegisterEquipment(AssembledEquipment equip)
        {
            if (equip == null) return;
            var modifiers = equip.GetTotalStatModifiers();
            ApplyStatModifiers(modifiers);
            appliedModifiers[equip] = modifiers;
            ApplyPassiveBuffs(equip);
            OnStatsChanged?.Invoke();
        }

        public void UnregisterEquipment(AssembledEquipment equip)
        {
            if (equip == null) return;
            if (appliedModifiers.TryGetValue(equip, out var modifiers))
            {
                RemoveStatModifiers(modifiers);
                appliedModifiers.Remove(equip);
            }
            RemovePassiveBuffs(equip);
            OnStatsChanged?.Invoke();
        }

        // ── 스탯 적용/제거 (컴포지션 콜백 경유) ──

        private void ApplyStatModifiers(List<EquipmentStatModifier> modifiers)
        {
            if (ApplyStat == null) return;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                ApplyStat(mod.statType, mod.modifierType, mod.value);
            }

            // ── [게임 연결 예시 — lLcrowe 원본] ─────────────────────────────
            //  콜백 대신 게임 스탯 모듈을 직접 호출하던 원본. 자기 시스템으로 교체.
            //
            //  if (abilityModule == null) return;
            //  for (int i = 0; i < modifiers.Count; i++)
            //  {
            //      var mod = modifiers[i];
            //      if (!abilityModule.GetUnitStatusValue(mod.statType, out var statusValue)) continue;
            //      float current = statusValue.GetFloatValue();
            //      switch (mod.modifierType)
            //      {
            //          case ModifierType.Flat:    abilityModule.SetUnitStatusValue(mod.statType, current + mod.value); break;
            //          case ModifierType.Percent: abilityModule.SetUnitStatusValue(mod.statType, current * (1f + mod.value / 100f)); break;
            //      }
            //  }
            // ────────────────────────────────────────────────────────────────
        }

        private void RemoveStatModifiers(List<EquipmentStatModifier> modifiers)
        {
            if (RemoveStat == null) return;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                RemoveStat(mod.statType, mod.modifierType, mod.value);
            }
        }

        // ── 파츠 스탯/버프 ──

        private void ApplyPartModifiers(PartData partData)
        {
            var c = partData.contribution;
            if (c == null || c.statModifiers == null) return;
            ApplyStatModifiers(new List<EquipmentStatModifier>(c.statModifiers));
        }

        private void RemovePartModifiers(PartData partData)
        {
            var c = partData.contribution;
            if (c == null || c.statModifiers == null) return;
            RemoveStatModifiers(new List<EquipmentStatModifier>(c.statModifiers));
        }

        private void ApplyPartBuffs(PartData partData)
        {
            var c = partData.contribution;
            if (c == null || c.partBuffs == null || ApplyBuff == null) return;
            for (int i = 0; i < c.partBuffs.Length; i++)
            {
                if (c.partBuffs[i] == null) continue;
                ApplyBuff(c.partBuffs[i]);
            }
            // [게임 연결 예시] BuffManager.Instance.AddBuff(abilityModule, c.partBuffs[i]);
        }

        private void RemovePartBuffs(PartData partData)
        {
            var c = partData.contribution;
            if (c == null || c.partBuffs == null || RemoveBuff == null) return;
            for (int i = 0; i < c.partBuffs.Length; i++)
            {
                if (c.partBuffs[i] == null) continue;
                RemoveBuff(c.partBuffs[i]);
            }
        }

        private void RefreshEquipmentStats(AssembledEquipment equip)
        {
            if (appliedModifiers.TryGetValue(equip, out var oldModifiers))
            {
                RemoveStatModifiers(oldModifiers);
                appliedModifiers.Remove(equip);
            }

            var newModifiers = equip.GetTotalStatModifiers();
            ApplyStatModifiers(newModifiers);
            appliedModifiers[equip] = newModifiers;

            equip.Recompute();
            OnStatsChanged?.Invoke();
        }

        // ── 패시브 버프 관리 ──

        private void ApplyPassiveBuffs(AssembledEquipment equip)
        {
            var buffs = equip.commonData.passiveBuffs;
            if (buffs == null || buffs.Length == 0 || ApplyBuff == null) return;

            var buffList = new List<ScriptableObject>();
            for (int i = 0; i < buffs.Length; i++)
            {
                if (buffs[i] == null) continue;
                ApplyBuff(buffs[i]);
                buffList.Add(buffs[i]);
            }
            if (buffList.Count > 0) appliedBuffs[equip] = buffList;
        }

        private void RemovePassiveBuffs(AssembledEquipment equip)
        {
            if (!appliedBuffs.TryGetValue(equip, out var buffList)) return;

            if (RemoveBuff != null)
            {
                for (int i = 0; i < buffList.Count; i++)
                {
                    if (buffList[i] == null) continue;
                    RemoveBuff(buffList[i]);
                }
            }
            appliedBuffs.Remove(equip);
        }
    }
}
