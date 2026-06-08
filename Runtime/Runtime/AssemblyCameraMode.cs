namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 조립 UI 카메라 모드.
    /// </summary>
    public enum AssemblyCameraMode
    {
        /// <summary>카메라가 장비 중심 공전 (우클릭 드래그)</summary>
        Orbital,

        /// <summary>카메라 고정, 장비가 회전 (좌클릭 드래그, 타르코프 스타일)</summary>
        ObjectRotate,

        /// <summary>슬롯 선택 시 해당 위치로 카메라 SmoothDamp 이동 (드레스룸 스타일)</summary>
        SlotFocus
    }
}
