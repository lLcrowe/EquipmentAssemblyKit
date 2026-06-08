namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 파츠 Tag 프리셋. 카테고리별로 사용 가능한 Tag 상수를 정의한다.
    /// string 기반이라 여기에 추가하면 에디터에서 자동으로 선택 가능.
    /// </summary>
    public static class PartTagPresets
    {
        // ── Frame (Core) ── 코어 파츠 = 장비의 시작점
        public const string Frame = "frame";

        // ── 총기 (Firearm) ──
        public const string BottomBody = "bottom_body";
        public const string TopBody = "top_body";
        public const string Barrel = "barrel";
        public const string Grip = "grip";
        public const string ForeGrip = "fore_grip";
        public const string Muzzle = "muzzle";
        public const string Accessory = "accessory";

        // ── 조준 (Optic) ──
        public const string Sight = "sight";
        public const string Scope = "scope";
        public const string Optic = "optic";
        public const string IronSight = "iron_sight";
        public const string RedDot = "red_dot";
        public const string Holographic = "holographic";
        public const string Laser = "laser";
        public const string Flashlight = "flashlight";

        // ── 근접무기 (Melee) ──
        public const string Blade = "blade";
        public const string Blunt = "blunt";
        public const string Handle = "handle";
        public const string Pommel = "pommel";
        public const string Guard = "guard";

        // ── 효과 (Effect) ──
        public const string Explosive = "explosive";
        public const string Fire = "fire";
        public const string Ice = "ice";
        public const string Poison = "poison";
        public const string Electric = "electric";
        public const string Holy = "holy";
        public const string Dark = "dark";

        // ── 방어구 (Defense) ──
        public const string ArmorPlate = "armor_plate";
        public const string Padding = "padding";
        public const string Reinforcement = "reinforcement";
        public const string Spike = "spike";
        public const string Shield = "shield";

        // ── 수납 (Container) ──
        public const string Pouch = "pouch";
        public const string Molle = "molle";
        public const string Pocket = "pocket";
        public const string Holster = "holster";
        public const string Sheath = "sheath";

        // ── 탄약 (Ammo) ──
        public const string Ammo = "ammo";
        public const string Bullet = "bullet";
        public const string Shell = "shell";
        public const string Rocket = "rocket";
        public const string Arrow = "arrow";
        public const string Energy = "energy";

        // ── 애니메이션 앵커 (Animation Anchor) ──
        public const string MainHand = "main_hand";
        public const string SubHand = "sub_hand";

        // ── 범용 (Utility) ──
        public const string Utility = "utility";
        public const string Medical = "medical";
        public const string Waterproof = "waterproof";
        public const string Lightweight = "lightweight";
        public const string HeavyDuty = "heavy_duty";

        /// <summary>
        /// 카테고리별 Tag 목록. PropertyDrawer에서 그룹핑 표시에 사용.
        /// 새 태그 추가 시 여기에도 등록해야 에디터에서 보임.
        /// Frame(Core)는 카테고리에 포함하지 않고 드롭다운 최상단에 단독 표시.
        /// </summary>
        public static readonly TagCategory[] Categories = new TagCategory[]
        {
            new TagCategory("총기", new[] {
                BottomBody, TopBody, Barrel, Grip, ForeGrip, Muzzle, Accessory
            }),
            new TagCategory("조준", new[] {
                Sight, Scope, Optic, IronSight, RedDot,
                Holographic, Laser, Flashlight
            }),
            new TagCategory("근접무기", new[] {
                Blade, Blunt, Handle, Pommel, Guard
            }),
            new TagCategory("효과", new[] {
                Explosive, Fire, Ice, Poison, Electric, Holy, Dark
            }),
            new TagCategory("방어구", new[] {
                ArmorPlate, Padding, Reinforcement, Spike, Shield
            }),
            new TagCategory("수납", new[] {
                Pouch, Molle, Pocket, Holster, Sheath
            }),
            new TagCategory("탄약", new[] {
                Ammo, Bullet, Shell, Rocket, Arrow, Energy
            }),
            new TagCategory("애니메이션", new[] {
                MainHand, SubHand
            }),
            new TagCategory("범용", new[] {
                Utility, Medical, Waterproof, Lightweight, HeavyDuty
            }),
        };
    }

}
