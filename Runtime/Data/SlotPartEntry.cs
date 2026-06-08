namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 슬롯-파츠 매핑 엔트리. 에디터 조립 결과를 직렬화한다.
    /// </summary>
    [System.Serializable]
    public struct SlotPartEntry
    {
        public string slotId;
        public PartInfo equippedPart;
    }
}
