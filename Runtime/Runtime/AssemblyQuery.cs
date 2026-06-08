using System.Collections.Generic;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 파츠 트리 검색 유틸리티. Stateless static.
    /// 태그 기반으로 장착된 파츠를 재귀 탐색한다.
    /// PartData[] 기반이므로 Equipment/Vehicle/Robot 등 모든 조립 시스템에서 공용.
    /// </summary>
    public static class AssemblyQuery
    {
        /// <summary>
        /// 파츠 배열에서 태그로 첫 번째 매칭 파츠를 찾는다 (깊이 우선).
        /// </summary>
        public static PartData FindPartByTag(PartData[] parts, string tag)
        {
            if (parts == null) return null;
            if (string.IsNullOrEmpty(tag)) return null;

            for (int i = 0; i < parts.Length; i++)
            {
                var found = FindInPartTree(parts[i], tag);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// 파츠 배열에서 태그로 모든 매칭 파츠를 찾는다.
        /// </summary>
        public static void FindAllPartsByTag(PartData[] parts, string tag, List<PartData> results)
        {
            if (parts == null) return;
            if (string.IsNullOrEmpty(tag)) return;
            if (results == null) return;

            for (int i = 0; i < parts.Length; i++)
            {
                CollectFromPartTree(parts[i], tag, results);
            }
        }

        /// <summary>
        /// 특정 파츠의 자식 트리에서만 태그로 찾는다.
        /// 자기 자신도 매칭 대상.
        /// </summary>
        public static PartData FindChildByTag(PartData parent, string tag)
        {
            if (parent == null) return null;
            if (string.IsNullOrEmpty(tag)) return null;

            return FindInPartTree(parent, tag);
        }

        /// <summary>
        /// connectToTag 기반으로 연결 대상 파츠를 찾는다.
        /// </summary>
        public static PartData FindConnectedPart(PartData[] parts, string connectToTag)
        {
            if (string.IsNullOrEmpty(connectToTag)) return null;

            return FindPartByTag(parts, connectToTag);
        }

        /// <summary>
        /// 슬롯 인덱스로 연결 대상을 찾는다.
        /// slots[slotIndex].connectToTag 사용.
        /// </summary>
        public static PartData FindConnectedPartBySlot(PartData[] parts, PartSlot[] slots, int slotIndex)
        {
            if (parts == null || slots == null) return null;
            if (slotIndex < 0 || slotIndex >= slots.Length) return null;

            string connectToTag = slots[slotIndex].connectToTag;
            return FindConnectedPart(parts, connectToTag);
        }

        /// <summary>
        /// 파츠에 특정 태그가 있는지 확인한다.
        /// </summary>
        public static bool HasTag(PartData part, string tag)
        {
            if (part == null || part.source == null) return false;
            if (string.IsNullOrEmpty(tag)) return false;

            var tags = part.source.partTags;
            if (tags == null) return false;

            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] == tag) return true;
            }

            return false;
        }

        // --- AnimAnchor 조회 ---

        /// <summary>
        /// animAnchor 태그로 장착된 파츠의 Transform을 찾는다.
        /// 슬롯의 animAnchor 필드를 검사하며, 해당 슬롯에 파츠가 장착되어 있고
        /// visualInstance가 존재할 때만 반환한다.
        /// </summary>
        public static Transform FindAnimAnchorTransform(PartData[] parts, PartSlot[] slots, in string anchorTag)
        {
            if (parts == null || slots == null) return null;
            if (string.IsNullOrEmpty(anchorTag)) return null;

            // 루트 슬롯 탐색
            int count = parts.Length < slots.Length ? parts.Length : slots.Length;
            for (int i = 0; i < count; i++)
            {
                if (slots[i].animAnchor == anchorTag)
                {
                    if (parts[i] != null && parts[i].visualInstance != null)
                        return parts[i].visualInstance.transform;
                }

                // 자식 재귀
                if (parts[i] == null) continue;
                var childResult = FindAnimAnchorInTree(parts[i], anchorTag);
                if (childResult != null) return childResult;
            }

            return null;
        }

        /// <summary>
        /// animAnchor 태그로 장착된 파츠의 위치와 회전을 가져온다.
        /// 오프셋을 적용한 최종 IK 타겟 좌표를 out으로 반환한다.
        /// </summary>
        public static bool TryGetAnimAnchorPose(
            PartData[] parts, PartSlot[] slots, in string anchorTag,
            in Vector3 positionOffset, in Vector3 rotationOffset,
            out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var tr = FindAnimAnchorTransform(parts, slots, anchorTag);
            if (tr == null) return false;

            rotation = tr.rotation * Quaternion.Euler(rotationOffset);
            position = tr.TransformPoint(positionOffset);
            return true;
        }

        /// <summary>
        /// 모든 animAnchor 매칭 결과를 수집한다 (NonAlloc).
        /// </summary>
        public static void FindAllAnimAnchors(PartData[] parts, PartSlot[] slots, List<AnimAnchorResult> results)
        {
            if (parts == null || slots == null || results == null) return;

            int count = parts.Length < slots.Length ? parts.Length : slots.Length;
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(slots[i].animAnchor)
                    && parts[i] != null
                    && parts[i].visualInstance != null)
                {
                    results.Add(new AnimAnchorResult
                    {
                        anchorTag = slots[i].animAnchor,
                        transform = parts[i].visualInstance.transform,
                        partData = parts[i],
                        slotIndex = i
                    });
                }

                // 자식 재귀
                if (parts[i] == null) continue;
                CollectAnimAnchorsFromTree(parts[i], results);
            }
        }

        // --- 내부 재귀 (AnimAnchor) ---

        private static Transform FindAnimAnchorInTree(PartData part, string anchorTag)
        {
            if (part.childParts == null || part.source == null) return null;
            var childSlots = part.source.childSlots;
            if (childSlots == null) return null;

            int count = part.childParts.Length < childSlots.Length
                ? part.childParts.Length : childSlots.Length;

            for (int i = 0; i < count; i++)
            {
                if (childSlots[i].animAnchor == anchorTag)
                {
                    if (part.childParts[i] != null && part.childParts[i].visualInstance != null)
                        return part.childParts[i].visualInstance.transform;
                }

                // 더 깊은 자식
                if (part.childParts[i] == null) continue;
                var found = FindAnimAnchorInTree(part.childParts[i], anchorTag);
                if (found != null) return found;
            }

            return null;
        }

        private static void CollectAnimAnchorsFromTree(PartData part, List<AnimAnchorResult> results)
        {
            if (part.childParts == null || part.source == null) return;
            var childSlots = part.source.childSlots;
            if (childSlots == null) return;

            int count = part.childParts.Length < childSlots.Length
                ? part.childParts.Length : childSlots.Length;

            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(childSlots[i].animAnchor)
                    && part.childParts[i] != null
                    && part.childParts[i].visualInstance != null)
                {
                    results.Add(new AnimAnchorResult
                    {
                        anchorTag = childSlots[i].animAnchor,
                        transform = part.childParts[i].visualInstance.transform,
                        partData = part.childParts[i],
                        slotIndex = i
                    });
                }

                if (part.childParts[i] == null) continue;
                CollectAnimAnchorsFromTree(part.childParts[i], results);
            }
        }

        // --- 내부 재귀 (Tag) ---

        private static PartData FindInPartTree(PartData part, string tag)
        {
            if (part == null) return null;

            // 자기 자신 체크
            if (HasTag(part, tag)) return part;

            // 자식 재귀
            if (part.childParts == null) return null;

            for (int i = 0; i < part.childParts.Length; i++)
            {
                var found = FindInPartTree(part.childParts[i], tag);
                if (found != null) return found;
            }

            return null;
        }

        private static void CollectFromPartTree(PartData part, string tag, List<PartData> results)
        {
            if (part == null) return;

            if (HasTag(part, tag))
            {
                results.Add(part);
            }

            if (part.childParts == null) return;

            for (int i = 0; i < part.childParts.Length; i++)
            {
                CollectFromPartTree(part.childParts[i], tag, results);
            }
        }
    }
}
