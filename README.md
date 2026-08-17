# Equipment Assembly Kit

*🇰🇷 [한국어 README](README.ko.md)*

A tag-based **modular part assembly system** for Unity. Snap parts into slots by tag to assemble equipment / vehicles / robots — visuals auto-attach and stats aggregate. A generic, reusable infrastructure.

- **Zero external dependency** — references only `UnityEngine`. Drops into any project as-is.
- **Game-agnostic** — game-specific logic (stats / buffs / combat) hooks in via **composition (Action callbacks) + partial extension**.

---

## Demo

[![Demo](https://img.youtube.com/vi/n2UT2t8MIcg/hqdefault.jpg)](https://www.youtube.com/watch?v=n2UT2t8MIcg)

---

## Core Concepts

| Concept | Type | Description |
|---------|------|-------------|
| Equipment | `EquipmentInfo` (SO) | Root slot holder. Common data + slot list |
| Part | `PartInfo` (SO) | Snaps into a slot. Recursive tree via `childSlots` |
| Slot | `PartSlot` | Accepted tags (`acceptedTags`), attach point, position/rotation |
| Contribution | `PartContribution` | What a part adds: stats / buffs / defense / visual / sound |
| Tag | `PartTagPresets` | string tags like `barrel`, `muzzle`, `armor_plate` |
| Controller | `AssemblyController<T>` | Assembly logic (equip/unequip/visual). Game application in a subclass |

**Assembly rule**: a part equips when the slot's `acceptedTags` and the part's `partTags` overlap. Parts hold further parts through their own `childSlots` (recursive).

---

## Installation

Import the `.unitypackage`, or copy the folder into `Packages/` or `Assets/`. The asmdef (`EquipmentAssemblyKit`) is auto-detected. No extra packages required (zero dependency).

---

## Quick Start

### 1. Create data (Editor)

```
Create > lLcroweTool/Equipment/Equipment Info   → Equipment SO
Create > lLcroweTool/Equipment/Part Info         → Part SO
```

- Add slots to `EquipmentInfo.rootSlots` → set each slot's `acceptedTags` (e.g. `barrel`)
- Set `PartInfo.partTags` (e.g. `barrel`) → assign a Renderer to `contribution.visualPrefab`

### 2. Runtime assembly

```csharp
using lLCroweTool.EquipmentAssemblyKit;

// Attach a controller (ExampleEquipmentController, or your own)
var controller = unitGO.AddComponent<ExampleEquipmentController>();

// Create an equipment instance
var equip = new AssembledEquipment(equipmentInfoSO);

// Equip a part into slot 0 (pass a visual holder to auto-place the mesh)
controller.AddPart(equip, slotIndex: 0, barrelPartInfo, visualHolder);
```

`AddPart` / `RemovePart` / `AddChildPart` / `ChangePartVariant` are the assembly API. Incompatible tags are rejected.

---

## Game Hookup (Composition)

You only wire up **how aggregated stats/buffs reach your game systems**. Two ways:

### (A) Inject Action callbacks — recommended

Register delegates on `ExampleEquipmentController`:

```csharp
var controller = unitGO.AddComponent<ExampleEquipmentController>();

controller.ApplyStat  = (statType, modType, value) => myStats.Add(statType, modType, value);
controller.RemoveStat = (statType, modType, value) => myStats.Remove(statType, modType, value);
controller.BindBuffCallbacks<MyBuff>(myBuffSystem.Add, myBuffSystem.Remove);

controller.RegisterEquipment(equip);   // applies stats/buffs via callbacks on register
```

- `statType` is a `string` ID (e.g. `"STAT_ATK"`) — map it to your game's stat key.
- `BindBuffCallbacks<TBuff>` validates the buff type, so the game hookup needs no cast. A mismatched SO type is reported as an error.

### (B) Clone the controller

Copy `ExampleEquipmentController` and fill the `[game hookup example]` comment spots with your own stat-module calls.

---

## Extension (partial)

Combat (damage / projectile / aim) and category data (weapon / armor) vary per game, so they're excluded from the core. **Add fields to the same class via `partial class`** — no inheritance, no interfaces.

Rename `PartContribution.GameExtension.cs.txt` to `.cs` and uncomment for an example:

```csharp
namespace lLCroweTool.EquipmentAssemblyKit
{
    public partial class PartContribution
    {
        public bool hasDamage;
        public MyDamagePartData damagePartData;   // your game's type
    }

    public partial class EquipmentInfo
    {
        public MyWeaponInfo weaponInfo;           // category data
    }
}
```

Core code (`AssemblyController`, `EquipmentAssembler`) works without knowing these extension fields. Aggregation/application is read from your own controller.

---

## Structure

```
Runtime/
├── Data/        EquipmentInfo · PartInfo · PartContribution · PartSlot · EquipmentCommonData
│                EquipmentStatModifier · SlotPartEntry · PartTagPresets · TagCategory · Enums
└── Runtime/     AssemblyController · AssemblyQuery · AssemblyAssembler · EquipmentAssembler
                 EquipmentVisualAssembler · EquipmentVisualHolder · PartConnectorVisual
                 PathPlacementModule · AssembledEquipment · PartData · ExampleEquipmentController
Editor/          Assembly window · inspector · tag dropdown · viewport · importers
```

- **Pure infrastructure** (assembly / visual / query): use as-is
- **Data**: game-agnostic via `string` / `ScriptableObject` slots
- **Extension**: `partial` + `Action` callbacks for game hookup

---

## License

MIT — see [LICENSE.md](LICENSE.md).

*by lLcrowe · EquipmentAssemblyKit v1.1.0*
