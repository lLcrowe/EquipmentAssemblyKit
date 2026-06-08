using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 조립 UI 공용 비주얼 설정. 장비/차량/로봇 등 모든 조립 시스템 UI에서 공유.
    /// </summary>
    [CreateAssetMenu(menuName = "lLCroweTool/Assembly UI Config")]
    public class AssemblyUIConfig : ScriptableObject
    {
        [Header("프리뷰 카메라")]
        public float defaultZoom = 3f;
        public Vector2 defaultOrbitAngles = new Vector2(15f, 0f);
        public float zoomMin = 1f;
        public float zoomMax = 10f;
        public float orbitSensitivity = 1f;
        public float zoomSensitivity = 1f;
        public Vector3 previewSpawnPosition = new Vector3(0, -100, 0);
        [Tooltip("프리뷰 전용 레이어. 조립 카메라 cullingMask + 스폰 오브젝트 레이어 설정에 사용")]
        public LayerMask previewLayer = 1 << 8;

        public AssemblyCameraMode cameraMode = AssemblyCameraMode.Orbital;

        [Header("슬롯 포커스 (SlotFocus 모드)")]
        [Tooltip("SmoothDamp 전환 시간 (초)")]
        public float focusTransitionSpeed = 0.3f;
        [Tooltip("포커스 시 추가 줌인 오프셋 (음수 = 줌인)")]
        public float focusZoomOffset = -0.5f;
        [Tooltip("물체 피봇 오프셋 (카메라 로컬 기준).\n음수X = 모델이 화면 좌측으로 이동.\n우측 사이드패널 때문에 왼쪽으로 밀 때: (-0.3, 0, 0)")]
        public Vector3 focusPivotOffset = Vector3.zero;
        public float orbitMouseMultiplier = 5f;
        public float zoomMouseMultiplier = 10f;
        public float pitchClampMin = -80f;
        public float pitchClampMax = 80f;
        public float initialYaw = 90f;

        [Header("슬롯 마커")]
        public Color emptySlotColor = Color.gray;
        public Color equippedSlotColor = Color.white;
        public Color selectedSlotColor = Color.cyan;
        public Color requiredSlotColor = Color.red;
        public float markerSize = 32f;
        [Range(0.1f, 1f)]
        public float markerDefaultAlpha = 0.5f;

        [Header("슬롯 트리")]
        public float slotItemHeight = 50f;
        public float slotIndentPixels = 24f;
        public Color slotPartNameColor = Color.white;
        public Color slotEmptyPartNameColor = new Color(1f, 1f, 1f, 0.5f);
        public Color slotEmptyBgColor = new Color(0.35f, 0.4f, 0.5f);
        public Color slotRequiredBgColor = new Color(0.8f, 0.3f, 0.3f);

        [Header("파츠 리스트")]
        [Tooltip("true = 호환 파츠만 표시, false = 전체 표시 (비호환은 반투명)")]
        public bool hideIncompatibleParts;
        public float partItemHeight = 60f;
        [Range(0f, 1f)]
        public float compatibleAlpha = 1f;
        [Range(0f, 1f)]
        public float incompatibleAlpha = 0.4f;

        [Header("사운드 폴백")]
        [Tooltip("파츠에 equipSounds가 없을 때 사용할 기본 장착 사운드")]
        public AudioClip[] fallbackEquipSounds = new AudioClip[0];
        [Tooltip("파츠에 unequipSounds가 없을 때 사용할 기본 해제 사운드")]
        public AudioClip[] fallbackUnequipSounds = new AudioClip[0];
    }
}
