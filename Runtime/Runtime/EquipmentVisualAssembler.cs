using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 장비 비주얼 조립 유틸리티. 에디터와 동일한 공식으로 파츠 위치/회전을 계산한다.
    /// Stateless static — EquipmentAssembler(스탯)와 대칭 구조.
    ///
    /// 조립 공식:
    ///   slotRot  = parentRot * Euler(slot.localRotation)
    ///   finalRot = slotRot * Euler(-visualRotationOffset)
    ///   finalPos = parentPos + parentRot * slot.localPosition - finalRot * visualOffset
    ///   자식 슬롯은 finalPos/finalRot 기준 재귀
    /// </summary>
    public static class EquipmentVisualAssembler
    {
        /// <summary>
        /// 파츠의 월드 위치/회전 계산. 에디터(PlaceEquippedPartRecursive)와 동일한 공식.
        /// contribution이 null이면 오프셋 없이 슬롯 위치만 계산 (슬롯 마커 표시용).
        /// </summary>
        public static void ComputePartTransform(
            Vector3 parentPos, Quaternion parentRot,
            in PartSlot slot, PartContribution contribution,
            out Vector3 finalPos, out Quaternion finalRot)
        {
            Quaternion slotRot = parentRot * Quaternion.Euler(slot.localRotation);

            Vector3 visualOffset = contribution != null ? contribution.visualOffset : Vector3.zero;
            Vector3 visualRotOffset = contribution != null ? contribution.visualRotationOffset : Vector3.zero;

            finalRot = slotRot * Quaternion.Euler(-visualRotOffset);
            finalPos = parentPos + parentRot * slot.localPosition - finalRot * visualOffset;
        }

        /// <summary>
        /// 파츠의 비주얼 프리팹을 결정한다 (기본 vs 변형).
        /// </summary>
        public static Renderer ResolveVisualPrefab(PartContribution contribution, int variantIndex)
        {
            if (contribution == null) return null;

            if (variantIndex >= 0 && contribution.variants != null && variantIndex < contribution.variants.Length)
                return contribution.variants[variantIndex];

            return contribution.visualPrefab;
        }

        /// <summary>
        /// 단일 파츠 비주얼을 공식 기반으로 생성 + 배치한다.
        /// parent를 부모 Transform으로 하되, 위치/회전은 공식으로 설정.
        /// </summary>
        public static GameObject InstantiatePartVisual(
            Vector3 parentPos, Quaternion parentRot,
            in PartSlot slot, PartContribution contribution,
            int variantIndex = -1, Transform parent = null)
        {
            var visual = ResolveVisualPrefab(contribution, variantIndex);
            if (visual == null) return null;

            ComputePartTransform(parentPos, parentRot, slot, contribution,
                out var finalPos, out var finalRot);

            // 부모 없이 생성하여 프리팹 원본 스케일 보존
            var instance = Object.Instantiate(visual.gameObject);
            instance.transform.position = finalPos;
            instance.transform.rotation = finalRot;

            // 월드 트랜스폼 유지한 채 부모로 편입
            if (parent != null)
            {
                instance.transform.SetParent(parent, true);
            }

            return instance;
        }

        /// <summary>
        /// PartData의 비주얼을 재귀적으로 배치한다.
        /// 자기 자신 + 모든 자식 파츠의 비주얼을 생성.
        /// </summary>
        public static void PlacePartRecursive(
            PartData partData, Vector3 parentPos, Quaternion parentRot,
            in PartSlot slot, Transform visualParent = null)
        {
            if (partData == null) return;

            var c = partData.contribution;
            if (c == null) return;

            // 기존 비주얼 제거
            DestroyVisualSingle(partData);

            // 비주얼 생성
            partData.visualInstance = InstantiatePartVisual(
                parentPos, parentRot, slot, c,
                partData.currentVariantIndex, visualParent);

            if (partData.visualInstance == null) return;

            // 최종 위치/회전 계산 (자식 재귀용)
            ComputePartTransform(parentPos, parentRot, slot, c,
                out var finalPos, out var finalRot);

            // 자식 파츠 재귀 배치
            if (partData.childParts != null && partData.source.childSlots != null)
            {
                for (int i = 0; i < partData.childParts.Length; i++)
                {
                    if (partData.childParts[i] == null) continue;
                    if (i >= partData.source.childSlots.Length) break;

                    PlacePartRecursive(
                        partData.childParts[i],
                        finalPos, finalRot,
                        partData.source.childSlots[i],
                        partData.visualInstance.transform);
                }
            }
        }

        /// <summary>
        /// 장비의 모든 파츠를 재귀적으로 비주얼 배치한다.
        /// 에디터의 RebuildAssemblyPreview와 동일한 역할.
        /// </summary>
        public static void RebuildAllVisuals(AssembledEquipment equip, Transform rootTransform)
        {
            if (equip == null || rootTransform == null) return;
            if (equip.parts == null || equip.source.rootSlots == null) return;

            int count = Mathf.Min(equip.parts.Length, equip.source.rootSlots.Length);

            for (int i = 0; i < count; i++)
            {
                if (equip.parts[i] == null) continue;

                PlacePartRecursive(
                    equip.parts[i],
                    rootTransform.position, rootTransform.rotation,
                    equip.source.rootSlots[i],
                    rootTransform);
            }
        }

        /// <summary>
        /// 파츠 + 자식 비주얼을 재귀적으로 제거한다.
        /// </summary>
        public static void DestroyPartVisualRecursive(PartData partData)
        {
            if (partData == null) return;

            // 자식 먼저 제거 (깊이 우선)
            if (partData.childParts != null)
            {
                for (int i = 0; i < partData.childParts.Length; i++)
                {
                    DestroyPartVisualRecursive(partData.childParts[i]);
                }
            }

            DestroyVisualSingle(partData);
        }

        /// <summary>
        /// 단일 비주얼 인스턴스 제거 (자식 재귀 없음).
        /// </summary>
        private static void DestroyVisualSingle(PartData partData)
        {
            if (partData == null || partData.visualInstance == null) return;

            if (Application.isPlaying)
            {
                Object.Destroy(partData.visualInstance);
            }
            else
            {
                Object.DestroyImmediate(partData.visualInstance);
            }

            partData.visualInstance = null;
        }
    }
}
