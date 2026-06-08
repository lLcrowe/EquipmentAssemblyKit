using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 제네릭 조립 컨트롤러 베이스. 태그 기반 파츠 트리 조립 인프라.
    /// 파츠 장착/해제, 비주얼 배치, 자식 파츠 재귀 관리를 담당한다.
    /// Equipment/Vehicle/Robot 등 모든 조립 시스템의 공용 베이스.
    /// 서브클래스는 GetRootSlots/GetParts로 데이터 접근을 제공하고,
    /// OnPartEquipped/OnPartUnequipped/OnAssemblyChanged 훅으로 도메인 로직을 처리한다.
    /// </summary>
    public abstract class AssemblyController<TAssembled> : MonoBehaviour where TAssembled : class
    {
        // --- 서브클래스 구현 (데이터 접근) ---

        /// <summary>
        /// 조립 대상의 루트 슬롯 배열을 반환한다.
        /// </summary>
        protected abstract PartSlot[] GetRootSlots(TAssembled assembled);

        /// <summary>
        /// 조립 대상의 장착 파츠 배열을 반환한다.
        /// </summary>
        protected abstract PartData[] GetParts(TAssembled assembled);

        // --- 서브클래스 훅 (도메인 로직) ---

        /// <summary>
        /// 파츠가 장착된 직후 호출. 스탯/버프 적용 등.
        /// 자식 파츠 개별 장착 시에도 호출된다.
        /// </summary>
        protected virtual void OnPartEquipped(TAssembled assembled, PartData partData) { }

        /// <summary>
        /// 파츠가 해제되기 직전 호출. 스탯/버프 역산 등.
        /// 자식 파츠 개별 해제 시에도 호출된다.
        /// </summary>
        protected virtual void OnPartUnequipped(TAssembled assembled, PartData partData) { }

        /// <summary>
        /// 조립 상태가 변경된 후 호출 (장착/해제 완료 시 1회).
        /// 전체 스탯 재계산, Recompute 등.
        /// </summary>
        protected virtual void OnAssemblyChanged(TAssembled assembled) { }

        // --- 공용 API ---

        /// <summary>
        /// 파츠 장착. Tag 호환성을 체크하고 슬롯에 파츠를 장착한다.
        /// </summary>
        public bool AddPart(TAssembled assembled, int slotIndex, PartInfo partItem,
                            EquipmentVisualHolder visualHolder = null, int variantIndex = -1)
        {
            if (assembled == null || partItem == null) return false;
            if (partItem.contribution == null) return false;

            var rootSlots = GetRootSlots(assembled);
            if (rootSlots == null) return false;
            if (slotIndex < 0 || slotIndex >= rootSlots.Length) return false;

            var slot = rootSlots[slotIndex];

            // Tag 호환성 체크
            if (!AssemblyAssembler.IsCompatible(slot, partItem))
                return false;

            var parts = GetParts(assembled);

            // 기존 파츠 있으면 먼저 해제
            if (parts[slotIndex] != null)
            {
                RemovePart(assembled, slotIndex, visualHolder);
            }

            // 파츠 생성 및 장착
            var partData = new PartData(partItem, slotIndex);
            parts[slotIndex] = partData;

            // 도메인 훅: 파츠 장착
            OnPartEquipped(assembled, partData);

            // 비주얼 부착 (공식 기반)
            if (visualHolder != null)
            {
                var c = partItem.contribution;
                var visual = EquipmentVisualAssembler.ResolveVisualPrefab(c, variantIndex);

                if (visual != null)
                {
                    partData.currentVariantIndex = variantIndex;
                    partData.visualInstance = visualHolder.AttachVisualWithFormula(
                        slot.attachPointId, visual.gameObject, slot, c);
                }
            }

            // 도메인 훅: 조립 상태 변경
            OnAssemblyChanged(assembled);

            return true;
        }

        /// <summary>
        /// 파츠 해제. 슬롯에서 파츠를 제거한다.
        /// 자식 파츠가 있으면 재귀적으로 제거한다.
        /// </summary>
        public PartData RemovePart(TAssembled assembled, int slotIndex,
                                   EquipmentVisualHolder visualHolder = null)
        {
            if (assembled == null) return null;

            var parts = GetParts(assembled);
            if (slotIndex < 0 || slotIndex >= parts.Length) return null;

            var partData = parts[slotIndex];
            if (partData == null) return null;

            // 자식 파츠 재귀 제거 (깊이 우선)
            RemoveChildPartsRecursive(assembled, partData);

            // 도메인 훅: 파츠 해제
            OnPartUnequipped(assembled, partData);

            // 비주얼 제거
            EquipmentVisualAssembler.DestroyPartVisualRecursive(partData);

            parts[slotIndex] = null;

            // 도메인 훅: 조립 상태 변경
            OnAssemblyChanged(assembled);

            return partData;
        }

        /// <summary>
        /// 파츠의 비주얼 변형을 교체한다. 스탯 변경 없이 외형만 변경.
        /// </summary>
        public bool ChangePartVariant(TAssembled assembled, int slotIndex, int variantIndex,
                                      EquipmentVisualHolder visualHolder)
        {
            if (assembled == null || visualHolder == null) return false;

            var parts = GetParts(assembled);
            if (slotIndex < 0 || slotIndex >= parts.Length) return false;

            var partData = parts[slotIndex];
            if (partData == null) return false;

            var c = partData.contribution;
            if (c == null || c.variants == null) return false;
            if (variantIndex < 0 || variantIndex >= c.variants.Length) return false;

            var rootSlots = GetRootSlots(assembled);
            var slot = rootSlots[slotIndex];

            // 기존 비주얼 제거
            EquipmentVisualAssembler.DestroyPartVisualRecursive(partData);

            // 새 비주얼 공식 기반 배치
            partData.visualInstance = visualHolder.AttachVisualWithFormula(
                slot.attachPointId, c.variants[variantIndex].gameObject, slot, c);
            partData.currentVariantIndex = variantIndex;

            // 자식 파츠 비주얼 재구축
            if (partData.childParts != null && partData.source.childSlots != null
                && partData.visualInstance != null)
            {
                EquipmentVisualAssembler.ComputePartTransform(
                    visualHolder.transform.position, visualHolder.transform.rotation,
                    slot, c, out var partPos, out var partRot);

                for (int i = 0; i < partData.childParts.Length; i++)
                {
                    if (partData.childParts[i] == null) continue;
                    if (i >= partData.source.childSlots.Length) break;

                    EquipmentVisualAssembler.PlacePartRecursive(
                        partData.childParts[i],
                        partPos, partRot,
                        partData.source.childSlots[i],
                        partData.visualInstance.transform);
                }
            }

            return true;
        }

        /// <summary>
        /// 파츠의 하위 슬롯에 자식 파츠를 장착한다.
        /// </summary>
        public bool AddChildPart(TAssembled assembled, PartData parentPart, int childSlotIndex,
                                 PartInfo childPartItem, int variantIndex = -1)
        {
            if (assembled == null || parentPart == null || childPartItem == null) return false;
            if (childPartItem.contribution == null) return false;
            if (parentPart.source.childSlots == null) return false;
            if (parentPart.childParts == null) return false;
            if (childSlotIndex < 0 || childSlotIndex >= parentPart.source.childSlots.Length) return false;

            var slot = parentPart.source.childSlots[childSlotIndex];

            // Tag 호환성 체크
            if (!AssemblyAssembler.IsCompatible(slot, childPartItem))
                return false;

            // 기존 자식 있으면 해제
            if (parentPart.childParts[childSlotIndex] != null)
            {
                RemoveChildPart(assembled, parentPart, childSlotIndex);
            }

            // 자식 파츠 생성 및 장착
            var childData = new PartData(childPartItem, childSlotIndex);
            parentPart.childParts[childSlotIndex] = childData;

            // 도메인 훅: 파츠 장착
            OnPartEquipped(assembled, childData);

            // 비주얼: 부모 파츠의 visualInstance 기준으로 배치
            if (parentPart.visualInstance != null)
            {
                var c = childPartItem.contribution;
                var visual = EquipmentVisualAssembler.ResolveVisualPrefab(c, variantIndex);
                if (visual != null)
                {
                    childData.currentVariantIndex = variantIndex;

                    EquipmentVisualAssembler.ComputePartTransform(
                        parentPart.visualInstance.transform.position,
                        parentPart.visualInstance.transform.rotation,
                        slot, c, out var finalPos, out var finalRot);

                    // 부모 없이 생성하여 프리팹 원본 스케일 보존
                    var instance = Instantiate(visual.gameObject);
                    instance.transform.position = finalPos;
                    instance.transform.rotation = finalRot;

                    // 월드 트랜스폼 유지한 채 부모로 편입
                    instance.transform.SetParent(parentPart.visualInstance.transform, true);
                    childData.visualInstance = instance;
                }
            }

            // 도메인 훅: 조립 상태 변경
            OnAssemblyChanged(assembled);

            return true;
        }

        /// <summary>
        /// 파츠의 하위 슬롯에서 자식 파츠를 해제한다.
        /// </summary>
        public PartData RemoveChildPart(TAssembled assembled, PartData parentPart, int childSlotIndex)
        {
            if (parentPart == null) return null;
            if (parentPart.childParts == null) return null;
            if (childSlotIndex < 0 || childSlotIndex >= parentPart.childParts.Length) return null;

            var childData = parentPart.childParts[childSlotIndex];
            if (childData == null) return null;

            // 자식의 자식 재귀 제거
            RemoveChildPartsRecursive(assembled, childData);

            // 도메인 훅: 파츠 해제
            OnPartUnequipped(assembled, childData);

            // 비주얼 제거
            EquipmentVisualAssembler.DestroyPartVisualRecursive(childData);

            parentPart.childParts[childSlotIndex] = null;

            // 도메인 훅: 조립 상태 변경
            OnAssemblyChanged(assembled);

            return childData;
        }

        /// <summary>
        /// 모든 파츠 비주얼을 재구축한다.
        /// 서브클래스에서 조립 대상별 비주얼 어셈블러를 호출할 수 있도록 virtual.
        /// </summary>
        public virtual void RebuildAllVisuals(TAssembled assembled, EquipmentVisualHolder visualHolder)
        {
            if (assembled == null || visualHolder == null) return;
            visualHolder.ClearAll();
        }

        // --- 내부 ---

        /// <summary>
        /// 파츠의 모든 자식을 재귀적으로 제거한다.
        /// 각 자식에 대해 OnPartUnequipped 훅을 호출한다.
        /// </summary>
        private void RemoveChildPartsRecursive(TAssembled assembled, PartData partData)
        {
            if (partData == null || partData.childParts == null) return;

            for (int i = 0; i < partData.childParts.Length; i++)
            {
                var child = partData.childParts[i];
                if (child == null) continue;

                // 손자 먼저 재귀 제거
                RemoveChildPartsRecursive(assembled, child);

                // 도메인 훅: 파츠 해제
                OnPartUnequipped(assembled, child);

                partData.childParts[i] = null;
            }
        }
    }
}
