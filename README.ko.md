# Equipment Assembly Kit

*🇬🇧 [English README](README.md)*

태그 기반 **모듈러 파츠 조립 시스템**. 슬롯에 태그로 파츠를 끼워 장비/차량/로봇 등을 조립하고, 비주얼이 자동으로 따라붙으며, 스탯이 합산되는 제네릭 인프라다.

- **외부 모듈 의존 0** — UnityEngine만 참조. 어느 프로젝트에든 그대로 들어간다.
- **게임 비종속** — 스탯/버프/전투 같은 게임별 로직은 **컴포지션(Action 콜백) + partial 확장**으로 연결한다.

---

## 데모

[![데모](https://img.youtube.com/vi/n2UT2t8MIcg/hqdefault.jpg)](https://www.youtube.com/watch?v=n2UT2t8MIcg)

---

## 핵심 개념

| 개념 | 타입 | 설명 |
|------|------|------|
| 장비 | `EquipmentInfo` (SO) | 루트 슬롯 보유자. 공통 데이터 + 슬롯 목록 |
| 파츠 | `PartInfo` (SO) | 슬롯에 끼우는 부품. `childSlots`로 재귀 트리 |
| 슬롯 | `PartSlot` | 허용 태그(`acceptedTags`), 부착점, 위치/회전 |
| 기여 | `PartContribution` | 파츠가 부모에 더하는 스탯/버프/방어/비주얼/사운드 |
| 태그 | `PartTagPresets` | `barrel`, `muzzle`, `armor_plate` 등 string 태그 |
| 컨트롤러 | `AssemblyController<T>` | 조립 로직(장착/해제/비주얼). 게임 적용은 서브클래스 |

**조립 규칙**: 슬롯의 `acceptedTags`와 파츠의 `partTags`가 호환되면 장착된다. 파츠는 자신의 `childSlots`로 또 다른 파츠를 받는다(재귀).

---

## 설치

`.unitypackage` Import 또는 폴더를 `Packages/` / `Assets/`에 복사. asmdef(`EquipmentAssemblyKit`)가 자동 인식된다. 외부 의존이 없어 추가 패키지 불필요.

---

## 빠른 시작

### 1. 데이터 만들기 (에디터)

```
Create > lLcroweTool/Equipment/Equipment Info   → 장비 SO
Create > lLcroweTool/Equipment/Part Info         → 파츠 SO
```

- `EquipmentInfo.rootSlots`에 슬롯 추가 → 각 슬롯의 `acceptedTags`에 받을 태그 지정 (예: `barrel`)
- `PartInfo.partTags`에 파츠 태그 지정 (예: `barrel`) → `contribution.visualPrefab`에 외형 Renderer 지정

### 2. 런타임 조립

```csharp
using lLCroweTool.EquipmentAssemblyKit;

// 컨트롤러 부착 (ExampleEquipmentController 또는 자기 컨트롤러)
var controller = unitGO.AddComponent<ExampleEquipmentController>();

// 장비 인스턴스 생성
var equip = new AssembledEquipment(equipmentInfoSO);

// 슬롯 0에 파츠 장착 (+ 비주얼 홀더 주면 외형 자동 배치)
controller.AddPart(equip, slotIndex: 0, barrelPartInfo, visualHolder);
```

`AddPart` / `RemovePart` / `AddChildPart` / `ChangePartVariant` 가 조립 API다. 태그가 안 맞으면 장착이 거부된다.

---

## 게임 연결 (컴포지션)

조립 결과로 나온 스탯/버프를 **자기 게임 시스템에 반영**하는 부분만 연결하면 된다. 두 방법:

### (A) Action 콜백 주입 — 권장

`ExampleEquipmentController`에 델리게이트를 등록한다.

```csharp
var controller = unitGO.AddComponent<ExampleEquipmentController>();

controller.ApplyStat  = (statType, modType, value) => myStats.Add(statType, modType, value);
controller.RemoveStat = (statType, modType, value) => myStats.Remove(statType, modType, value);
controller.BindBuffCallbacks<MyBuff>(myBuffSystem.Add, myBuffSystem.Remove);

controller.RegisterEquipment(equip);   // 등록 시 콜백으로 스탯/버프 적용
```

- `statType`은 `string` ID(예: `"STAT_ATK"`). 자기 게임 스탯 키에 매핑한다.
- `BindBuffCallbacks<TBuff>`가 버프 타입을 검사해 연결하므로 게임 연결부에 캐스팅이 필요 없다. 잘못된 SO 타입은 오류로 보고된다.

### (B) 컨트롤러 복제

`ExampleEquipmentController`를 복사해, 주석으로 표기된 `[게임 연결 예시]` 자리를 자기 스탯 모듈 호출로 채운다.

---

## 확장 (partial)

전투(대미지/투사체/조준)나 카테고리 데이터(무기/방어구)는 게임마다 달라 코어에서 제외했다. **`partial class`로 같은 클래스에 필드를 덧붙인다** — 상속도 인터페이스도 없이.

`PartContribution.GameExtension.cs.txt`를 `.cs`로 바꾸고 주석을 풀면 예시가 나온다:

```csharp
namespace lLCroweTool.EquipmentAssemblyKit
{
    public partial class PartContribution
    {
        public bool hasDamage;
        public MyDamagePartData damagePartData;   // 자기 게임 타입
    }

    public partial class EquipmentInfo
    {
        public MyWeaponInfo weaponInfo;           // 카테고리별 데이터
    }
}
```

코어 코드(`AssemblyController`, `EquipmentAssembler`)는 이 확장 필드를 몰라도 그대로 동작한다. 합산·적용은 자기 컨트롤러에서 읽어 처리한다.

---

## 구조

```
Runtime/
├── Data/        EquipmentInfo·PartInfo·PartContribution·PartSlot·EquipmentCommonData
│                EquipmentStatModifier·SlotPartEntry·PartTagPresets·TagCategory·Enums
└── Runtime/     AssemblyController·AssemblyQuery·AssemblyAssembler·EquipmentAssembler
                 EquipmentVisualAssembler·EquipmentVisualHolder·PartConnectorVisual
                 PathPlacementModule·AssembledEquipment·PartData·ExampleEquipmentController
Editor/          조립 윈도우 · 인스펙터 · 태그 드롭다운 · 뷰포트 · 임포터
```

- **순수 인프라**(조립/비주얼/검색): 손대지 않고 그대로 사용
- **데이터**: `string`/`ScriptableObject` 슬롯으로 게임 비종속화
- **확장**: `partial` + `Action` 콜백으로 게임 연결

---

## 라이선스

MIT — [LICENSE.md](LICENSE.md) 참조.

*by lLcrowe · EquipmentAssemblyKit v1.1.0*
