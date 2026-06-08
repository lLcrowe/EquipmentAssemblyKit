namespace lLCroweTool.EquipmentAssemblyKit
{
    public enum SlotType
    {
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        MainHand,
        OffHand,
        Belt,
        Back,
        Accessory
    }

    /// <summary>
    /// 장착 슬롯 타입 프리셋 (string 기반, 에디터 드롭다운용).
    /// EquipmentCommonData.slotType에 저장.
    /// </summary>
    public static class SlotTypePresets
    {
        public const string Head = "head";
        public const string Chest = "chest";
        public const string Legs = "legs";
        public const string Feet = "feet";
        public const string Hands = "hands";
        public const string MainHand = "main_hand";
        public const string OffHand = "off_hand";
        public const string Belt = "belt";
        public const string Back = "back";
        public const string Accessory = "accessory";

        public static readonly string[] All = new[]
        {
            Head, Chest, Legs, Feet, Hands,
            MainHand, OffHand, Belt, Back, Accessory
        };
    }
}
