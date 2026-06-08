using lLCroweTool.EquipmentAssemblyKit;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// 장비 어셈블러 에디터 v2.
    /// 탭별 독립 3패널: 조립(코어브라우저|작업|요약) / 워크숍(파츠라이브러리|편집|요약)
    /// </summary>
    public class EquipmentInfoWindowEditor : EditorWindow
    {
        // ── 범용 모듈 ──
        private EditorViewport viewport = new EditorViewport();
        private EditorGizmoRenderer gizmo = new EditorGizmoRenderer();
        private EditorPanelLayout panelLayout = new EditorPanelLayout();
        private EditorTabSystem<MainTab> tabSystem;

        // ── 탭 ──
        enum MainTab { Assembly, Workshop }

        // ── 데이터 ──
        private List<EquipmentInfo> dataList = new List<EquipmentInfo>();
        private List<PartInfo> partDataList = new List<PartInfo>();

        // ── 조립 상태 ──
        private EquipmentInfo selectedEquipment; // 조립 프로젝트 (.equipment 파일)
        private PartInfo selectedCore;           // 코어 파츠 (조립 시작점)
        private Dictionary<string, PartInfo> equipped = new Dictionary<string, PartInfo>();
        private Dictionary<string, Transform> equippedPartTransforms = new Dictionary<string, Transform>();
        private string selectedSlotId;
        private string outputSOName = "";
        private Vector2 asmBrowserScroll, asmWorkScroll, asmSummaryScroll;
        private string asmSearchFilter = "";
        private Dictionary<string, bool> treeCollapsed = new Dictionary<string, bool>();
        private string asmPartFilter = "";
        private string asmSnapshot;
        private bool asmIsDirty;

        // ── 조립 computed 캐시 ──
        private bool needRecompute = true;
        private List<EquipmentStatModifier> partModifiers = new List<EquipmentStatModifier>();

        // ── 워크숍 상태 ──
        private static PartInfo mainEquipment;
        private SerializedObject mainSerializedObj;
        private Vector2 wsBrowserScroll, wsEditorScroll, wsSummaryScroll;
        private string wsSearchFilter = "";
        private int wsSelectedSlotIdx = -1;
        private bool wsShowBasic = true, wsShowVisual = true, wsShowSlots = true, wsShowContrib = true;
        private string wsSnapshot;
        private bool wsIsDirty;

        // ── 상태표시 ──
        private string lastLogMessage = "";

        // ── Renderer 프리팹 피커 ──
        private int visualPrefabPickerControlId;
        private enum VisualPickerTarget { Workshop, Contrib }
        private VisualPickerTarget visualPickerTarget;
        private SerializedProperty visualPickerContribProp;

        // ── 색상 ──
        private static readonly Color IncreaseColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color DecreaseColor = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color RequiredColor = new Color(1f, 0.6f, 0.2f);
        private static readonly Color CoreBadgeColor = new Color(0.7f, 0.42f, 0.13f);
        private static readonly Color TabActiveColor = new Color(0.82f, 0.44f, 0.16f);
        private static readonly Color TreeGuideColor = new Color(0.12f, 0.2f, 0.25f);
        private static readonly Color HoverRowColor = new Color(1f, 1f, 1f, 0.06f);

        // ── 상수 ──
        private const float SLOT_HIT_THRESHOLD = 15f;                   // 슬롯 클릭 판정 거리 (px)

        // ── 경로 ──
        private const string PARTS_SAVE_FOLDER = "Assets/Data/EQ/Parts";

        [MenuItem("lLcroweTool/EquipmentTool/EquipmentInfoWindowEditor")]
        public static void ShowWindow()
        {
            var window = GetWindow<EquipmentInfoWindowEditor>();
            window.titleContent = new GUIContent("장비 어셈블러");
            window.minSize = new Vector2(900, 500);
        }

        public static void SetLoadData(PartInfo data)
        {
            mainEquipment = data;
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            RefreshDataList();

            // 범용 모듈 초기화
            viewport.Initialize();
            viewport.onRepaint = Repaint;
            viewport.onMouseDown = OnViewportMouseDown;
            viewport.onMouseDrag = OnViewportMouseDrag;
            viewport.onMouseUp = OnViewportMouseUp;

            gizmo.Initialize(viewport);

            panelLayout.onRepaint = Repaint;

            tabSystem = new EditorTabSystem<MainTab>(MainTab.Assembly, new[] { "\ud83d\udd29 조립", "\ud83d\udd27 파츠" });
            tabSystem.onTabChanged = OnTabChanged;
        }

        private void OnDisable()
        {
            viewport.Dispose();
        }

        private void OnFocus()
        {
            RefreshDataList();
        }

        private void RefreshDataList()
        {
            dataList.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(EquipmentInfo)}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<EquipmentInfo>(path);
                if (asset != null) dataList.Add(asset);
            }

            partDataList.Clear();
            string[] partGuids = AssetDatabase.FindAssets($"t:{nameof(PartInfo)}");
            for (int i = 0; i < partGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(partGuids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<PartInfo>(path);
                if (asset != null) partDataList.Add(asset);
            }
        }

        private void AddLog(string message)
        {
            lastLogMessage = message;
        }

        // ============================
        // OnGUI
        // ============================

        private void OnGUI()
        {
            if (needRecompute && selectedCore != null)
            {
                RecomputeStats();
                needRecompute = false;
            }

            DrawHeader();
            tabSystem.DrawTabBar(TabActiveColor);

            EditorGUILayout.BeginHorizontal();
            switch (tabSystem.currentTab)
            {
                case MainTab.Assembly: DrawAssemblyTab(); break;
                case MainTab.Workshop: DrawWorkshopTab(); break;
            }
            EditorGUILayout.EndHorizontal();

            DrawStatusBar();
            HandleKeyboardInput();
            HandleVisualPrefabPicker();
        }

        // ============================
        // Header
        // ============================

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("◈", GUILayout.Width(16));
            GUILayout.Label("장비 어셈블러", EditorStyles.boldLabel, GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            if (tabSystem.currentTab == MainTab.Assembly && selectedEquipment != null)
            {
                string eqName = !string.IsNullOrEmpty(selectedEquipment.equipmentInfo.displayName)
                    ? selectedEquipment.equipmentInfo.displayName : selectedEquipment.name;
                string coreName = selectedCore != null
                    ? (!string.IsNullOrEmpty(selectedCore.name)
                        ? selectedCore.name : selectedCore.name)
                    : "미선택";
                GUILayout.Label($"[장비] {eqName} → [코어] {coreName}", EditorStyles.miniLabel);
            }
            else if (tabSystem.currentTab == MainTab.Workshop && mainEquipment != null)
            {
                string name = !string.IsNullOrEmpty(mainEquipment.name)
                    ? mainEquipment.name : mainEquipment.name;
                GUILayout.Label($"[편집] {name}", EditorStyles.miniLabel);

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(35)))
                    EditorGUIUtility.PingObject(mainEquipment);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ============================
        // 탭/뷰포트 콜백
        // ============================

        private void OnTabChanged(MainTab prev, MainTab next)
        {
            if (next == MainTab.Assembly && selectedCore != null)
                RebuildAssemblyPreview();
            else if (next == MainTab.Workshop && mainEquipment != null)
                RebuildWorkshopPreview();
        }

        private void OnViewportMouseDown(Event e, Rect rect)
        {
            // Workshop 좌클릭: 슬롯 히트 테스트
            if (tabSystem.currentTab == MainTab.Workshop)
            {
                int hitIdx = HitTestWorkshopSlots(rect, e.mousePosition);
                if (hitIdx >= 0)
                {
                    viewport.dragSlotIdx = hitIdx;
                    wsSelectedSlotIdx = hitIdx;
                }
                else
                {
                    viewport.dragSlotIdx = -1;
                }
            }
        }

        private void OnViewportMouseDrag(Event e, Rect rect, Vector2 delta)
        {
            // Workshop 슬롯 드래그
            if (tabSystem.currentTab == MainTab.Workshop && viewport.dragSlotIdx >= 0)
            {
                ApplySlotDrag(rect, delta);
            }
        }

        private void OnViewportMouseUp(Event e)
        {
            viewport.dragSlotIdx = -1;
        }

        // ================================================================
        // 조립 탭
        // ================================================================

        private void DrawAssemblyTab()
        {
            panelLayout.BeginLeftPanel();
            DrawAssemblyBrowser();
            panelLayout.EndLeftPanel();

            panelLayout.BeginCenterPanel();
            DrawAssemblyWorkspace();
            panelLayout.EndCenterPanel();

            panelLayout.BeginRightPanel();
            DrawAssemblySummary();
            panelLayout.EndRightPanel();
        }

        // ── 조립: 좌측 코어 브라우저 ──

        private void DrawAssemblyBrowser()
        {
            // 헤더
            EditorGUILayout.LabelField("장비 프로젝트", EditorStyles.miniBoldLabel);
            asmSearchFilter = EditorGUILayout.TextField(asmSearchFilter, EditorStyles.toolbarSearchField);

            // CRUD
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("신규", EditorStyles.miniButton)) CreateNewEquipmentInfo();
            GUI.enabled = selectedEquipment != null;
            if (GUILayout.Button("복제", EditorStyles.miniButton)) DuplicateEquipmentInfo();
            if (GUILayout.Button("삭제", EditorStyles.miniButton)) DeleteEquipmentInfo();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 목록 (.equipment 확장자만)
            asmBrowserScroll = EditorGUILayout.BeginScrollView(asmBrowserScroll);
            int browserCount = 0;
            for (int i = 0; i < dataList.Count; i++)
            {
                var data = dataList[i];
                if (data == null) continue;
                if (!IsEquipmentExtension(data)) continue;
                if (!MatchesSearch(data, asmSearchFilter)) continue;

                browserCount++;
                bool isSel = selectedEquipment == data;
                string displayName = !string.IsNullOrEmpty(data.equipmentInfo.displayName)
                    ? data.equipmentInfo.displayName : data.name;
                string partId = data.equipmentInfo.id;

                // 카드
                EditorGUILayout.BeginVertical(isSel ? "SelectionRect" : EditorStyles.helpBox);

                var nameStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal
                };
                GUILayout.Label(displayName, nameStyle);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);
                GUILayout.Label(partId, EditorStyles.miniLabel, GUILayout.Width(80));
                string coreLabel = selectedEquipment == data && selectedCore != null
                    ? selectedCore.name : "—";
                GUILayout.Label($"코어:{coreLabel}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // 카드 전체 영역 클릭
                Rect cardRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
                {
                    SelectEquipmentProject(data);
                    Event.current.Use();
                }
            }

            if (browserCount == 0 && !string.IsNullOrEmpty(asmSearchFilter))
            {
                GUILayout.Label("검색 결과 없음", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndScrollView();

            // 새로고침 + JSON
            EditorGUILayout.Space(2);
            if (GUILayout.Button("새로고침", EditorStyles.miniButton)) RefreshDataList();
        }

        // ── 조립: 중앙 작업 공간 ──

        private void DrawAssemblyWorkspace()
        {
            if (selectedEquipment == null)
            {
                EditorGUILayout.HelpBox("← 좌측에서 장비 프로젝트를 선택하세요.", MessageType.Info);
                return;
            }

            // ── 1. 헤더: 이름 + 저장/원복 (스크롤 밖 — 항상 보임) ──
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            {
                string eqName = !string.IsNullOrEmpty(selectedEquipment.equipmentInfo.displayName)
                    ? selectedEquipment.equipmentInfo.displayName : selectedEquipment.name;
                string asmDirtyMark = asmIsDirty ? " *" : "";
                GUILayout.Label(eqName + asmDirtyMark, EditorStyles.boldLabel);
                GUILayout.Label(selectedEquipment.equipmentInfo.id, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUI.enabled = asmIsDirty;
                if (GUILayout.Button("저장", EditorStyles.miniButton, GUILayout.Width(40)))
                    SaveAssemblyState();
                if (GUILayout.Button("원복", EditorStyles.miniButton, GUILayout.Width(40)))
                    RevertAssemblyState();
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // ── 2. 뷰포트 ──
            DrawViewport(ViewportMode.Assembly);

            EditorGUILayout.Space(3);

            // ── 3. 스크롤 영역: 조립 구성 + 파츠 선택 ──
            asmWorkScroll = EditorGUILayout.BeginScrollView(asmWorkScroll);

            // 섹션 타이틀
            EditorGUILayout.LabelField("조립 구성도", EditorStyles.miniBoldLabel);

            // 코어 미선택 → 코어 선택 리스트
            if (selectedCore == null)
            {
                DrawCoreSelectionStep();
            }
            else
            {
                // ── 코어 루트 노드 ──
                string coreName = !string.IsNullOrEmpty(selectedCore.name)
                    ? selectedCore.name : selectedCore.name;
                int slotCount = selectedCore.childSlots != null ? selectedCore.childSlots.Length : 0;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                // 접기
                bool coreCollapsed = treeCollapsed.ContainsKey("__core__") && treeCollapsed["__core__"];
                if (GUILayout.Button(coreCollapsed ? "▸" : "▾", EditorStyles.miniButton, GUILayout.Width(16)))
                    treeCollapsed["__core__"] = !coreCollapsed;

                // 코어 뱃지
                var prevC = GUI.color;
                GUI.color = CoreBadgeColor;
                GUILayout.Label("코어", EditorStyles.miniButton, GUILayout.Width(30));
                GUI.color = prevC;

                GUILayout.Label(coreName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{slotCount}슬롯", EditorStyles.miniLabel);

                // 코어 제거
                if (GUILayout.Button("✕", EditorStyles.miniButton,
                    GUILayout.Width(18), GUILayout.Height(15)))
                {
                    selectedCore = null;
                    equipped.Clear();
                    selectedSlotId = null;
                    needRecompute = true;
                    ClearPreviewInstances();
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                // ── 자식 슬롯 (들여쓰기) ──
                if (!coreCollapsed && selectedCore.childSlots != null)
                {
                    for (int i = 0; i < selectedCore.childSlots.Length; i++)
                    {
                        DrawHierarchyNode(selectedCore.childSlots[i], 1,
                            i == selectedCore.childSlots.Length - 1, i);
                    }
                }

                // 파츠 선택 (항상 타이틀+검색 표시, 그리드는 슬롯 선택 시)
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("파츠 선택", EditorStyles.miniBoldLabel);
                DrawPartGrid();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAssemblyHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 코어 정보
            var c = GUI.color;
            GUI.color = CoreBadgeColor;
            GUILayout.Label("코어", EditorStyles.miniButton, GUILayout.Width(30));
            GUI.color = c;

            string coreName = !string.IsNullOrEmpty(selectedCore.name)
                ? selectedCore.name : selectedCore.name;
            GUILayout.Label(coreName, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            int totalSlots = selectedCore.childSlots != null ? selectedCore.childSlots.Length : 0;
            int equippedCount = 0;
            if (selectedCore.childSlots != null)
            {
                for (int i = 0; i < selectedCore.childSlots.Length; i++)
                    if (equipped.ContainsKey(GetSlotKey(selectedCore.childSlots[i], i))) equippedCount++;
            }

            GUILayout.Label($"{selectedCore.weight}kg", EditorStyles.miniLabel);
            c = GUI.color;
            GUI.color = equippedCount > 0 ? IncreaseColor : Color.gray;
            GUILayout.Label($"{equippedCount}/{totalSlots} 슬롯", EditorStyles.miniBoldLabel);
            GUI.color = c;

            EditorGUILayout.EndHorizontal();
        }

        private void SaveAssemblyState()
        {
            if (selectedEquipment == null) return;

            // equipped 딕셔너리 → assembledParts 배열로 변환
            var entries = new System.Collections.Generic.List<SlotPartEntry>();
            foreach (var kvp in equipped)
            {
                entries.Add(new SlotPartEntry
                {
                    slotId = kvp.Key,
                    equippedPart = kvp.Value
                });
            }
            selectedEquipment.assembledParts = entries.ToArray();
            selectedEquipment.assembledCore = selectedCore;

            EditorUtility.SetDirty(selectedEquipment);
            SaveIfCustomExtension(selectedEquipment);
            AssetDatabase.SaveAssets();

            asmSnapshot = JsonUtility.ToJson(selectedEquipment);
            asmIsDirty = false;

            string name = !string.IsNullOrEmpty(selectedEquipment.equipmentInfo.displayName)
                ? selectedEquipment.equipmentInfo.displayName : selectedEquipment.name;
            AddLog($"[저장] {name} (파츠 {entries.Count}개)");
        }

        private void RevertAssemblyState()
        {
            if (selectedEquipment == null) return;

            // 스냅샷 기반 복원 (확장자 무관)
            if (!string.IsNullOrEmpty(asmSnapshot))
            {
                JsonUtility.FromJsonOverwrite(asmSnapshot, selectedEquipment);
            }
            else if (IsCustomExtensionAsset(selectedEquipment))
            {
                string assetPath = AssetDatabase.GetAssetPath(selectedEquipment);
                string absPath = UnityPathToAbsolute(assetPath);
                if (absPath != null && System.IO.File.Exists(absPath))
                {
                    string json = System.IO.File.ReadAllText(absPath);
                    JsonUtility.FromJsonOverwrite(json, selectedEquipment);
                }
            }

            // assembledParts → equipped 딕셔너리로 복원
            LoadAssemblyState();
            asmIsDirty = false;

            string name = !string.IsNullOrEmpty(selectedEquipment.equipmentInfo.displayName)
                ? selectedEquipment.equipmentInfo.displayName : selectedEquipment.name;
            AddLog($"[원복] {name}");
            RebuildAssemblyPreview();
        }

        private void LoadAssemblyState()
        {
            equipped.Clear();
            selectedSlotId = null;
            needRecompute = true;

            if (selectedEquipment == null) return;

            // assembledCore 복원
            if (selectedEquipment.assembledCore != null)
            {
                selectedCore = selectedEquipment.assembledCore;
                string cn = !string.IsNullOrEmpty(selectedCore.name)
                    ? selectedCore.name : selectedCore.name;
                outputSOName = cn + "_조립";
            }

            // assembledParts → equipped 딕셔너리 복원
            if (selectedEquipment.assembledParts != null)
            {
                for (int i = 0; i < selectedEquipment.assembledParts.Length; i++)
                {
                    var entry = selectedEquipment.assembledParts[i];
                    if (!string.IsNullOrEmpty(entry.slotId) && entry.equippedPart != null)
                    {
                        equipped[entry.slotId] = entry.equippedPart;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════
        // ── PreviewRenderUtility 초기화 / 정리
        // ══════════════════════════════════════════════════

        private void ClearPreviewInstances()
        {
            equippedPartTransforms.Clear();
            viewport.ClearInstances();
        }

        private GameObject InstantiateInPreview(GameObject source)
        {
            return viewport.Instantiate(source);
        }

        // ══════════════════════════════════════════════════
        // ── 프리뷰 인스턴스 구축
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 조립 탭: 코어 + 장착 파츠 전체 구축
        /// </summary>
        private void RebuildAssemblyPreview(bool autoFrame = true)
        {
            ClearPreviewInstances();
            equippedPartTransforms.Clear();
            if (viewport.Preview == null || selectedCore == null) return;

            var coreVisual = selectedCore.contribution != null
                ? selectedCore.contribution.visualPrefab : null;

            if (coreVisual == null) return;

            var coreGo = InstantiateInPreview(coreVisual.gameObject);
            coreGo.transform.position = Vector3.zero;
            coreGo.transform.rotation = Quaternion.identity;

            // 2D 판별
            viewport.is2D = coreGo.GetComponentInChildren<SpriteRenderer>() != null;

            // 장착된 파츠들 재귀 배치
            if (selectedCore.childSlots != null)
            {
                for (int i = 0; i < selectedCore.childSlots.Length; i++)
                {
                    PlaceEquippedPartRecursive(coreGo.transform, selectedCore.childSlots[i], i);
                }
            }

            // 카메라 자동 프레이밍 (초기 로드만)
            if (autoFrame) AutoFramePreview();
        }

        /// <summary>
        /// 워크숍 탭: 단일 파츠 구축
        /// </summary>
        private void RebuildWorkshopPreview()
        {
            ClearPreviewInstances();
            if (viewport.Preview == null || mainEquipment == null) return;

            var visual = mainEquipment.contribution != null
                ? mainEquipment.contribution.visualPrefab : null;

            if (visual == null) return;

            var go = InstantiateInPreview(visual.gameObject);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            viewport.is2D = go.GetComponentInChildren<SpriteRenderer>() != null;
            AutoFramePreview();
        }

        /// <summary>Transform 기반 진입점 (루트 호출용)</summary>
        private void PlaceEquippedPartRecursive(Transform parentTransform, PartSlot slot, int index, string parentKey = "")
        {
            if (parentTransform == null) return;
            PlaceEquippedPartRecursive(parentTransform.position, parentTransform.rotation, slot, index, parentKey);
        }

        /// <summary>
        /// 조립 공식: 부모위치 + 부모회전*slot.localPos + 슬롯회전*visualOffset
        /// 자식 슬롯은 파츠 최종 위치/회전(visualOffset 포함) 기준
        /// </summary>
        private void PlaceEquippedPartRecursive(Vector3 parentPos, Quaternion parentRot, PartSlot slot, int index, string parentKey = "")
        {
            string slotKey = GetSlotKey(slot, index, parentKey);
            PartInfo ep = null;
            if (!equipped.TryGetValue(slotKey, out ep)) return;

            var partVisual = ep.contribution != null ? ep.contribution.visualPrefab : null;
            if (partVisual == null) return;

            // 슬롯 회전
            Quaternion slotRot = parentRot * Quaternion.Euler(slot.localRotation);

            // 비주얼 오프셋
            Vector3 visualOffset = ep.contribution != null ? ep.contribution.visualOffset : Vector3.zero;
            Vector3 visualRotOffset = ep.contribution != null ? ep.contribution.visualRotationOffset : Vector3.zero;

            // 최종 회전 먼저 계산 (오프셋 방향 기준)
            Quaternion finalRot = slotRot * Quaternion.Euler(-visualRotOffset);

            // 최종 위치: 부모위치 + 부모회전*슬롯위치 - 파츠회전*파츠오프셋
            Vector3 finalPos = parentPos + parentRot * slot.localPosition - finalRot * visualOffset;

            var partGo = InstantiateInPreview(partVisual.gameObject);
            partGo.transform.position = finalPos;
            partGo.transform.rotation = finalRot;
            viewport.Instances.Add(partGo);
            equippedPartTransforms[slotKey] = partGo.transform;

            // 하위 슬롯은 파츠 최종 위치/회전 기준 재귀
            if (ep.childSlots != null)
            {
                for (int i = 0; i < ep.childSlots.Length; i++)
                    PlaceEquippedPartRecursive(finalPos, finalRot, ep.childSlots[i], i, slotKey);
            }
        }

        private void AutoFramePreview()
        {
            viewport.AutoFrame();
        }

        // ══════════════════════════════════════════════════
        // ── 뷰포트 그리기 (EditorViewport 위임)
        // ══════════════════════════════════════════════════

        private enum ViewportMode { Assembly, Workshop }

        private void DrawViewport(ViewportMode mode)
        {
            // 프리팹 유무 판정
            bool hasPrefab = false;
            if (mode == ViewportMode.Assembly)
                hasPrefab = selectedCore != null && selectedCore.contribution != null
                    && selectedCore.contribution.visualPrefab != null;
            else
                hasPrefab = mainEquipment != null && mainEquipment.contribution != null
                    && mainEquipment.contribution.visualPrefab != null;

            // 뷰포트 Rect 확보 + 빈 화면/리사이즈 핸들 처리
            Rect rect = viewport.DrawViewport(hasPrefab, null, null);
            if (!hasPrefab || viewport.Preview == null) return;

            // Icon 버튼 영역 예약
            Rect iconBtnRect = Rect.zero;
            if (mode == ViewportMode.Workshop)
                iconBtnRect = new Rect(rect.x + 4, rect.y + 4, 36, 16);

            // 렌더링 (GL 마커 + GUI 오버레이)
            viewport.RenderViewport(rect,
                // onRenderGL: GL 마커
                () =>
                {
                    if (mode == ViewportMode.Assembly)
                        DrawAssemblyMarkers();
                    else
                        DrawWorkshopMarkers();
                },
                // onGUIOverlay: 2D 라벨 + Icon 버튼
                (r) =>
                {
                    if (mode == ViewportMode.Assembly)
                        DrawAssemblySlotLabels(r);
                    else
                        DrawWorkshopSlotLabels(r);

                    // Icon 버튼
                    if (mode == ViewportMode.Workshop)
                    {
                        if (GUI.Button(iconBtnRect, "Icon", EditorStyles.miniButton))
                            CaptureViewportAsIcon(r);
                    }
                },
                iconBtnRect
            );
        }

        private void CaptureViewportAsIcon(Rect rect)
        {
            if (viewport.Preview == null || mainEquipment == null) return;

            // 마커 없이 캡처
            Texture2D captured = viewport.CaptureStaticPreview(rect);
            if (captured == null)
            {
                AddLog("[아이콘] 캡처 실패");
                return;
            }

            // 64x64 리사이즈
            RenderTexture prev = RenderTexture.active;
            RenderTexture scaled = RenderTexture.GetTemporary(64, 64);
            Graphics.Blit(captured, scaled);
            RenderTexture.active = scaled;
            Texture2D icon64 = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            icon64.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            icon64.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(scaled);
            DestroyImmediate(captured);

            // PNG 저장 (경로 포워드슬래시 통일)
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(mainEquipment));
            if (string.IsNullOrEmpty(folder)) folder = "Assets";
            folder = folder.Replace('\\', '/');
            string fileName = mainEquipment.name + "_icon.png";
            string path = folder + "/" + fileName;
            byte[] pngBytes = icon64.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, pngBytes);
            DestroyImmediate(icon64);

            // 1차 임포트
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            // Sprite 타입으로 변경
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64;
                importer.maxTextureSize = 64;
                importer.SaveAndReimport();
            }
            else
            {
                AddLog($"[아이콘] TextureImporter null — path: {path}");
                return;
            }

            // Sprite 할당
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Sprite sp) { sprite = sp; break; }
            }

            if (sprite != null)
            {
                mainEquipment.icon = sprite;
                MarkWorkshopDirty();
                AssetDatabase.SaveAssets();
                AddLog($"[아이콘] {fileName} 저장 + 할당 완료");
            }
            else
            {
                AddLog($"[아이콘] {fileName} Sprite 로딩 실패 — path: {path}");
            }
        }

        // ── 슬롯 위치 헬퍼 ──

        private Vector3 GetSlotWorldPosition(Transform parent, PartSlot slot)
        {
            return parent.position + parent.rotation * slot.localPosition;
        }

        private Quaternion GetSlotWorldRotation(Transform parent, PartSlot slot)
        {
            return parent.rotation * Quaternion.Euler(slot.localRotation);
        }

        // ── 마우스 입력: EditorViewport가 처리, 콜백으로 위임 ──

        private int HitTestWorkshopSlots(Rect rect, Vector2 mousePos)
        {
            if (mainEquipment == null || mainEquipment.childSlots == null) return -1;
            if (viewport.Instances.Count == 0) return -1;

            Transform parent = viewport.Instances[0].transform;
            Camera cam = viewport.Preview.camera;
            float closestDist = SLOT_HIT_THRESHOLD;
            int closestIdx = -1;

            for (int i = 0; i < mainEquipment.childSlots.Length; i++)
            {
                Vector3 worldPos = GetSlotWorldPosition(parent, mainEquipment.childSlots[i]);
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                if (screenPos.z <= 0f) continue;

                float guiX = rect.x + (screenPos.x / cam.pixelWidth) * rect.width;
                float guiY = rect.yMax - (screenPos.y / cam.pixelHeight) * rect.height;
                float dist = Vector2.Distance(new Vector2(guiX, guiY), mousePos);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestIdx = i;
                }
            }

            return closestIdx;
        }

        private void ApplySlotDrag(Rect rect, Vector2 mouseDelta)
        {
            if (mainEquipment == null || mainEquipment.childSlots == null) return;
            if (viewport.dragSlotIdx < 0 || viewport.dragSlotIdx >= mainEquipment.childSlots.Length) return;
            if (viewport.Instances.Count == 0) return;

            Transform parent = viewport.Instances[0].transform;
            Vector3 worldDelta;

            if (viewport.is2D)
            {
                float worldPerPixel = viewport.zoom / rect.height;
                worldDelta = new Vector3(mouseDelta.x * worldPerPixel, -mouseDelta.y * worldPerPixel, 0f);
            }
            else
            {
                Camera cam = viewport.Preview.camera;
                Vector3 camRight = cam.transform.right;
                Vector3 camUp = cam.transform.up;
                float scale = viewport.zoom * 0.003f;
                worldDelta = (mouseDelta.x * camRight - mouseDelta.y * camUp) * scale;
            }

            // 월드 델타 → 로컬 델타
            Vector3 localDelta = Quaternion.Inverse(parent.rotation) * worldDelta;

            var slot = mainEquipment.childSlots[viewport.dragSlotIdx];
            slot.localPosition += localDelta;
            mainEquipment.childSlots[viewport.dragSlotIdx] = slot;
            MarkWorkshopDirty();
        }

        // ── GL 마커 그리기 ──

        private void DrawAssemblyMarkers()
        {
            if (selectedCore == null || selectedCore.childSlots == null) return;
            if (viewport.Instances.Count == 0) return;

            Transform coreTransform = viewport.Instances[0].transform;

            for (int i = 0; i < selectedCore.childSlots.Length; i++)
            {
                DrawSlotMarkerRecursive(coreTransform, selectedCore.childSlots[i], true, i);
            }
        }

        private void DrawWorkshopMarkers()
        {
            if (mainEquipment == null || mainEquipment.childSlots == null) return;
            if (viewport.Instances.Count == 0) return;

            Transform partTransform = viewport.Instances[0].transform;

            // ── 슬롯 마커 ──
            for (int i = 0; i < mainEquipment.childSlots.Length; i++)
            {
                var slot = mainEquipment.childSlots[i];
                Vector3 pos = GetSlotWorldPosition(partTransform, slot);
                Quaternion rot = GetSlotWorldRotation(partTransform, slot);
                bool isSel = wsSelectedSlotIdx == i;
                bool isDrag = viewport.dragSlotIdx == i;

                Color markerColor;
                if (isDrag) markerColor = Color.white;
                else if (isSel) markerColor = new Color(1f, 0.9f, 0.2f);
                else if (slot.isRequired) markerColor = RequiredColor;
                else markerColor = new Color(0.5f, 0.5f, 0.5f);

                float scale = EditorViewport.MARKER_RADIUS * viewport.zoom * 0.4f;
                if (isSel || isDrag) scale *= 1.3f;
                Color wireColor = (isSel || isDrag) ? Color.white : markerColor * 1.3f;

                gizmo.DrawSlotMarker(pos, rot, scale, markerColor, wireColor, viewport.is2D);

                if (slot.isRequired && !isSel && !isDrag)
                    gizmo.DrawRequiredRing(pos, rot, scale, RequiredColor);

                gizmo.DrawConnectionLine(partTransform.position, pos, markerColor);
            }

            // ── 피봇 기즈모 ──
            {
                Vector3 origin = partTransform.position;
                Quaternion pRot = partTransform.rotation;
                float scale = EditorViewport.MARKER_RADIUS * viewport.zoom * 0.25f;

                gizmo.DrawPivotMarker(origin, pRot, scale,
                    new Color(0.2f, 0.9f, 0.4f), new Color(0.3f, 0.5f, 1f), viewport.is2D);

                // 오프셋 기즈모
                if (mainEquipment.contribution != null)
                {
                    Vector3 posOffset = mainEquipment.contribution.visualOffset;
                    Vector3 rotOffset = mainEquipment.contribution.visualRotationOffset;
                    bool hasPos = posOffset.sqrMagnitude > 0.0001f;
                    bool hasRot = rotOffset.sqrMagnitude > 0.0001f;

                    if (hasPos || hasRot)
                    {
                        Vector3 pivotPos = hasPos ? origin + pRot * posOffset : origin;
                        Quaternion pivotRot = hasRot ? pRot * Quaternion.Euler(rotOffset) : pRot;

                        gizmo.DrawOffsetMarker(origin, pivotPos, pivotRot, scale,
                            new Color(0.4f, 1f, 0.5f), viewport.is2D, hasPos);
                    }
                }
            }
        }

        private void DrawSlotMarkerRecursive(Transform parentTransform, PartSlot slot, bool isAssembly, int index, string parentKey = "")
        {
            if (parentTransform == null) return;
            string key = GetSlotKey(slot, index, parentKey);
            DrawSlotMarker(parentTransform, slot, isAssembly, index, parentKey);

            PartInfo ep = null;
            if (equipped.TryGetValue(key, out ep) && ep.childSlots != null)
            {
                Transform partTransform;
                if (!equippedPartTransforms.TryGetValue(key, out partTransform) || partTransform == null)
                    partTransform = parentTransform;
                for (int i = 0; i < ep.childSlots.Length; i++)
                {
                    DrawSlotMarkerRecursive(partTransform, ep.childSlots[i], isAssembly, i, key);
                }
            }
        }

        private void DrawSlotMarker(Transform parentTransform, PartSlot slot, bool isAssembly, int index, string parentKey = "")
        {
            if (parentTransform == null) return;
            Vector3 pos = GetSlotWorldPosition(parentTransform, slot);
            Quaternion rot = GetSlotWorldRotation(parentTransform, slot);

            string key = GetSlotKey(slot, index, parentKey);
            Color markerColor;
            bool isEquipped = isAssembly && equipped.ContainsKey(key);
            bool isSel = isAssembly ? selectedSlotId == key : false;
            if (isSel) markerColor = new Color(1f, 0.9f, 0.2f);
            else if (isEquipped) markerColor = IncreaseColor;
            else if (slot.isRequired) markerColor = RequiredColor;
            else markerColor = new Color(0.5f, 0.5f, 0.5f);

            float scale = EditorViewport.MARKER_RADIUS * viewport.zoom * 0.15f;
            Color wireColor = isSel ? Color.white : markerColor * 1.3f;

            gizmo.DrawFilledDisk(pos, rot, scale, markerColor);
            gizmo.DrawWireCircle(pos, rot, scale, wireColor);
            gizmo.DrawConnectionLine(parentTransform.position, pos, markerColor);
        }

        private Transform FindAttachPointOrFallback(Transform parent, string attachPointId)
        {
            if (!string.IsNullOrEmpty(attachPointId))
            {
                var found = parent.Find(attachPointId);
                if (found != null) return found;
            }
            return parent;
        }

        // ── 슬롯 라벨 (GUI 오버레이) ──

        private void DrawAssemblySlotLabels(Rect viewportRect)
        {
            if (selectedCore == null || selectedCore.childSlots == null) return;
            if (viewport.Instances.Count == 0) return;

            Transform coreTransform = viewport.Instances[0].transform;
            for (int i = 0; i < selectedCore.childSlots.Length; i++)
            {
                DrawSlotLabelRecursive(viewportRect, coreTransform, selectedCore.childSlots[i], true, i);
            }
        }

        private void DrawWorkshopSlotLabels(Rect viewportRect)
        {
            if (mainEquipment == null || mainEquipment.childSlots == null) return;
            if (viewport.Instances.Count == 0) return;

            Transform partTransform = viewport.Instances[0].transform;
            Camera cam = viewport.Preview.camera;

            for (int i = 0; i < mainEquipment.childSlots.Length; i++)
            {
                var slot = mainEquipment.childSlots[i];
                Vector3 worldPos = GetSlotWorldPosition(partTransform, slot);
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                if (screenPos.z <= 0f) continue;

                float labelX = viewportRect.x + (screenPos.x / cam.pixelWidth) * viewportRect.width;
                float labelY = viewportRect.yMax - (screenPos.y / cam.pixelHeight) * viewportRect.height;

                // 라벨: 태그 목록
                var tags = slot.acceptedTags;
                string label = (tags != null && tags.Length > 0) ? string.Join(", ", tags) : "(태그 없음)";

                bool isSel = wsSelectedSlotIdx == i;
                Color textColor = isSel ? Color.yellow : new Color(0.8f, 0.8f, 0.8f);

                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 9,
                    fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = textColor },
                    wordWrap = false
                };

                float labelW = 80f;
                float labelH = 14f;
                Rect labelRect = new Rect(labelX - labelW * 0.5f, labelY + 6f, labelW, labelH);

                Color bgColor = new Color(0f, 0f, 0f, isSel ? 0.8f : 0.6f);
                EditorGUI.DrawRect(new Rect(labelRect.x - 1, labelRect.y - 1,
                    labelRect.width + 2, labelRect.height + 2), bgColor);

                GUI.Label(labelRect, label, style);
            }
        }

        private void DrawSlotLabelRecursive(Rect viewportRect, Transform parentTransform,
            PartSlot slot, bool isAssembly, int index, string parentKey = "")
        {
            if (parentTransform == null) return;
            string key = GetSlotKey(slot, index, parentKey);
            DrawSlotLabel(viewportRect, parentTransform, slot, isAssembly, index, parentKey);
            PartInfo ep = null;
            if (equipped.TryGetValue(key, out ep) && ep.childSlots != null)
            {
                Transform partTransform;
                if (!equippedPartTransforms.TryGetValue(key, out partTransform) || partTransform == null)
                    partTransform = parentTransform;
                for (int i = 0; i < ep.childSlots.Length; i++)
                {
                    DrawSlotLabelRecursive(viewportRect, partTransform, ep.childSlots[i], isAssembly, i, key);
                }
            }
        }

        private void DrawSlotLabel(Rect viewportRect, Transform parentTransform,
            PartSlot slot, bool isAssembly, int index, string parentKey = "")
        {
            if (parentTransform == null) return;
            Vector3 worldPos = GetSlotWorldPosition(parentTransform, slot);

            // 월드→스크린 변환
            Vector3 screenPos = viewport.Preview.camera.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0f) return; // 카메라 뒤

            // 스크린→뷰포트 Rect 변환
            float labelX = viewportRect.x + (screenPos.x / viewport.Preview.camera.pixelWidth) * viewportRect.width;
            float labelY = viewportRect.yMax - (screenPos.y / viewport.Preview.camera.pixelHeight) * viewportRect.height;

            // 슬롯 이름
            string label = GetSlotDisplayLabel(slot, index);
            if (slot.acceptedTags != null && slot.acceptedTags.Length > 0
                && !string.IsNullOrEmpty(slot.slotId))
            {
                label += "\n" + string.Join(", ", slot.acceptedTags);
            }

            // 색상
            string key = GetSlotKey(slot, index, parentKey);
            bool isSel = isAssembly ? selectedSlotId == key : false;
            Color textColor = isSel ? Color.yellow : new Color(0.8f, 0.8f, 0.8f);

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 9,
                fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = textColor },
                wordWrap = false
            };

            float labelW = 80f;
            float labelH = slot.acceptedTags != null && slot.acceptedTags.Length > 0 ? 24f : 14f;
            Rect labelRect = new Rect(labelX - labelW * 0.5f, labelY + 6f, labelW, labelH);

            // 배경
            Color bgColor = new Color(0f, 0f, 0f, 0.6f);
            EditorGUI.DrawRect(new Rect(labelRect.x - 1, labelRect.y - 1,
                labelRect.width + 2, labelRect.height + 2), bgColor);

            GUI.Label(labelRect, label, style);
        }

        // ── 하이어라키 트리 (재귀) ──

        /// <summary>슬롯 표시 이름 생성. slotId → 태그 → 인덱스 폴백</summary>
        private string GetSlotDisplayLabel(PartSlot slot, int index)
        {
            if (!string.IsNullOrEmpty(slot.slotId))
                return slot.slotId;
            if (slot.acceptedTags != null && slot.acceptedTags.Length > 0)
                return string.Join("/", slot.acceptedTags);
            return $"Slot #{index}";
        }

        /// <summary>슬롯 고유 키. 내부 식별용 (equipped, selectedSlotId).
        /// parentKey를 포함하면 경로 기반 키 생성 (깊이 2+ 충돌 방지)</summary>
        private string GetSlotKey(PartSlot slot, int index, string parentKey = "")
        {
            string localKey = !string.IsNullOrEmpty(slot.slotId)
                ? slot.slotId
                : $"__idx_{index}";
            return string.IsNullOrEmpty(parentKey) ? localKey : $"{parentKey}.{localKey}";
        }

        private void DrawHierarchyNode(PartSlot slot, int depth, bool isLast, int index, string parentKey = "")
        {
            string slotKey = GetSlotKey(slot, index, parentKey);
            string slotLabel = GetSlotDisplayLabel(slot, index);

            PartInfo ep = null;
            equipped.TryGetValue(slotKey, out ep);

            bool hasChildren = ep != null && ep.childSlots != null && ep.childSlots.Length > 0;
            bool isSel = selectedSlotId == slotKey;

            float indentW = 16f;

            // 들여쓰기
            if (depth > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(depth * indentW);
            }

            // 카드 배경 (선택 시 하이라이트)
            var prevBg = GUI.backgroundColor;
            if (isSel) GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();

            // 접기 토글 (자식 있을 때) / 트리 커넥터
            if (hasChildren)
            {
                bool collapsed = treeCollapsed.ContainsKey(slotKey) && treeCollapsed[slotKey];
                if (GUILayout.Button(collapsed ? "▸" : "▾", EditorStyles.miniButton, GUILayout.Width(16)))
                    treeCollapsed[slotKey] = !collapsed;
            }
            else
            {
                GUILayout.Label(isLast ? "└" : "├", EditorStyles.miniLabel, GUILayout.Width(14));
            }

            // 필수/선택 마크
            var prevColor = GUI.color;
            if (slot.isRequired) GUI.color = RequiredColor;
            else GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label(slot.isRequired ? "●" : "○", EditorStyles.miniLabel, GUILayout.Width(12));
            GUI.color = prevColor;

            // 슬롯 이름 (Label 사용 — 텍스트 렌더 보장)
            GUILayout.Label(slotLabel, isSel ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.MinWidth(60));

            // 장착 파츠명
            if (ep != null)
            {
                string pn = ep.name;
                prevColor = GUI.color;
                GUI.color = IncreaseColor;
                GUILayout.Label("→ " + pn, EditorStyles.miniLabel);
                GUI.color = prevColor;
            }

            GUILayout.FlexibleSpace();

            // ✕ 제거 버튼 (장착 시)
            if (ep != null)
            {
                if (GUILayout.Button("✕", EditorStyles.miniButton,
                    GUILayout.Width(18), GUILayout.Height(15)))
                {
                    RemovePart(slotKey);
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // ── 카드 클릭 → 슬롯 선택/해제 ──
            Rect cardRect = GUILayoutUtility.GetLastRect();
            if (depth > 0)
            {
                // GetLastRect는 Vertical(helpBox) 기준 — indent 보정
                cardRect.x += depth * indentW;
            }
            if (Event.current.type == EventType.MouseDown
                && cardRect.Contains(Event.current.mousePosition))
            {
                selectedSlotId = isSel ? null : slotKey;
                Event.current.Use();
                Repaint();
            }

            if (depth > 0)
                EditorGUILayout.EndHorizontal();

            // 자식 트리 (재귀)
            if (hasChildren && !(treeCollapsed.ContainsKey(slotKey) && treeCollapsed[slotKey]))
            {
                for (int i = 0; i < ep.childSlots.Length; i++)
                {
                    DrawHierarchyNode(ep.childSlots[i], depth + 1,
                        i == ep.childSlots.Length - 1, i, slotKey);
                }
            }
        }

        // ── 선택 슬롯 상세 ──

        private void DrawSelectedSlotDetail()
        {
            PartSlot slot;
            if (!FindSlotInTree(selectedCore, selectedSlotId, out slot)) return;

            PartInfo ep = null;
            equipped.TryGetValue(selectedSlotId, out ep);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(selectedSlotId, EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label(slot.isRequired ? "● 필수" : "○ 선택",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = slot.isRequired ? RequiredColor : Color.gray }
                });
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 허용 태그
            EditorGUILayout.LabelField("허용 태그", EditorStyles.miniLabel);
            if (slot.acceptedTags != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);
                for (int t = 0; t < slot.acceptedTags.Length; t++)
                    GUILayout.Label(slot.acceptedTags[t], EditorStyles.miniButton);
                EditorGUILayout.EndHorizontal();
            }

            // 장착 파츠 기여값
            if (ep != null && ep.contribution != null)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("장착 파츠 기여값", EditorStyles.miniLabel);
                DrawPartSummary(ep.contribution);
            }

            // 장착된 파츠 제거 버튼
            if (ep != null)
            {
                EditorGUILayout.Space(3);
                string epName = ep.name;

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
                if (GUILayout.Button($"파츠 제거 ({epName})", EditorStyles.miniButton))
                {
                    RemovePart(selectedSlotId);
                    GUI.backgroundColor = oldBg;
                    GUIUtility.ExitGUI();
                    return;
                }
                GUI.backgroundColor = oldBg;
            }

            EditorGUILayout.EndVertical();
        }

        // ── 하단 파츠 그리드 ──

        private Vector2 asmPartGridScroll;

        private void DrawPartGrid(float areaHeight = 0f)
        {
            // 구분선
            var sep = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1));
            EditorGUI.DrawRect(sep, new Color(0.15f, 0.2f, 0.25f));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("파츠", EditorStyles.miniBoldLabel, GUILayout.Width(30));
            asmPartFilter = EditorGUILayout.TextField(asmPartFilter,
                EditorStyles.toolbarSearchField, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(selectedSlotId))
            {
                PartSlot selSlot;
                if (FindSlotInTree(selectedCore, selectedSlotId, out selSlot)
                    && selSlot.acceptedTags != null)
                {
                    int tagShow = Mathf.Min(3, selSlot.acceptedTags.Length);
                    string tagStr = string.Join(" · ", selSlot.acceptedTags, 0, tagShow);
                    GUILayout.Label($"{tagStr} 필터중", EditorStyles.miniLabel);
                }
            }
            else
            {
                GUILayout.Label("슬롯 선택 후 클릭 장착", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // 필터링된 파츠 목록
            // 슬롯 미선택 시 그리드 숨김
            if (string.IsNullOrEmpty(selectedSlotId))
            {
                GUILayout.Label("조립 구성도에서 슬롯을 선택하세요", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(3);
                return;
            }

            string[] slotTags = null;
            {
                PartSlot sel;
                if (FindSlotInTree(selectedCore, selectedSlotId, out sel))
                    slotTags = sel.acceptedTags;
            }

            // 스크롤 영역 (높이 지정 시)
            if (areaHeight > 0f)
            {
                float scrollH = Mathf.Max(40f, areaHeight - 25f);
                asmPartGridScroll = EditorGUILayout.BeginScrollView(asmPartGridScroll,
                    GUILayout.Height(scrollH));
            }

            EditorGUILayout.BeginHorizontal();
            int count = 0;
            float gridW = position.width - 40f;
            float itemW = 100f;
            int cols = Mathf.Max(1, Mathf.FloorToInt(gridW / itemW));

            for (int i = 0; i < partDataList.Count; i++)
            {
                var part = partDataList[i];
                if (part == null) continue;
                if (!IsPartAsset(part)) continue;
                if (IsCoreAsset(part)) continue;

                // 태그 필터
                if (slotTags != null)
                {
                    bool match = false;
                    var partTags = part.partTags;
                    for (int t = 0; t < slotTags.Length && !match; t++)
                        for (int p = 0; p < partTags.Length && !match; p++)
                            if (slotTags[t] == partTags[p]) match = true;
                    if (!match) continue;
                }

                // 검색 필터
                if (!string.IsNullOrEmpty(asmPartFilter) && !MatchesSearch(part, asmPartFilter))
                    continue;

                // 장착 여부
                bool isEquippedHere = !string.IsNullOrEmpty(selectedSlotId)
                    && equipped.ContainsKey(selectedSlotId)
                    && equipped[selectedSlotId] == part;

                if (count > 0 && count % cols == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                // 파츠 카드
                var bgColor = isEquippedHere ? new Color(0.1f, 0.25f, 0.1f) :
                    slotTags != null ? new Color(0.1f, 0.15f, 0.2f) : new Color(0.08f, 0.1f, 0.13f);
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = bgColor;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(itemW - 4));
                GUI.backgroundColor = oldBg;

                string pName = part.name;
                var nameCol = isEquippedHere ? IncreaseColor :
                    slotTags != null ? new Color(0.5f, 0.6f, 0.7f) : new Color(0.3f, 0.35f, 0.4f);
                GUILayout.Label(pName, new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = nameCol },
                    fontStyle = isEquippedHere ? FontStyle.Bold : FontStyle.Normal
                });

                // 태그 미니뱃지
                EditorGUILayout.BeginHorizontal();
                var tags = part.partTags;
                float cardMaxW = itemW - 12f;
                float usedW = 0f;
                int shown = 0;
                for (int t = 0; t < tags.Length; t++)
                {
                    string tag = tags[t];
                    float tagW = EditorStyles.miniButton.CalcSize(new GUIContent(tag)).x + 2f;
                    if (usedW + tagW > cardMaxW && shown > 0) break;
                    GUILayout.Label(tag, EditorStyles.miniButton, GUILayout.Width(tagW));
                    usedW += tagW + 2f;
                    shown++;
                }
                if (shown < tags.Length)
                    GUILayout.Label($"+{tags.Length - shown}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // 클릭 → 장착
                if (Event.current.type == EventType.MouseDown
                    && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    if (string.IsNullOrEmpty(selectedSlotId))
                    {
                        AddLog("[안내] 슬롯을 먼저 선택하세요");
                    }
                    else
                    {
                        EquipPart(selectedSlotId, part);
                    }
                    Event.current.Use();
                    Repaint();
                }

                count++;
            }
            EditorGUILayout.EndHorizontal();

            if (count == 0)
            {
                string emptyMsg;
                if (!string.IsNullOrEmpty(asmPartFilter))
                    emptyMsg = "검색 결과 없음";
                else if (slotTags != null)
                    emptyMsg = "호환 파츠 없음";
                else
                    emptyMsg = "파츠 없음";
                GUILayout.Label(emptyMsg, EditorStyles.centeredGreyMiniLabel);
            }

            if (areaHeight > 0f)
                EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(3);
        }

        // ── 조립: 우측 요약 ──

        private void DrawAssemblySummary()
        {
            EditorGUILayout.LabelField("요약", EditorStyles.miniBoldLabel);

            if (selectedCore == null)
            {
                EditorGUILayout.HelpBox("장비를 선택하세요.", MessageType.Info);
                return;
            }

            asmSummaryScroll = EditorGUILayout.BeginScrollView(asmSummaryScroll);

            // 코어 이름/ID/무게
            string coreName = !string.IsNullOrEmpty(selectedCore.name)
                ? selectedCore.name : selectedCore.name;
            GUILayout.Label(coreName, EditorStyles.boldLabel);
            GUILayout.Label(selectedCore.name, EditorStyles.miniLabel);
            GUILayout.Label($"{selectedCore.weight} kg", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // 슬롯 진행률
            int total = selectedCore.childSlots != null ? selectedCore.childSlots.Length : 0;
            int eqCount = 0;
            if (selectedCore.childSlots != null)
                for (int i = 0; i < selectedCore.childSlots.Length; i++)
                    if (equipped.ContainsKey(GetSlotKey(selectedCore.childSlots[i], i))) eqCount++;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("슬롯 배치", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            var c = GUI.color;
            GUI.color = eqCount > 0 ? IncreaseColor : Color.gray;
            GUILayout.Label($"{eqCount}/{total}", EditorStyles.miniBoldLabel);
            GUI.color = c;
            EditorGUILayout.EndHorizontal();

            // 프로그레스바
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(4));
            EditorGUI.DrawRect(barRect, new Color(0.08f, 0.12f, 0.16f));
            if (total > 0)
            {
                float pct = (float)eqCount / total;
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * pct, 4),
                    IncreaseColor);
            }

            EditorGUILayout.Space(3);

            // 장착된 파츠 목록
            EditorGUILayout.LabelField("조립 파츠", EditorStyles.miniLabel);
            DrawEquippedPartsList(selectedCore, 0);

            EditorGUILayout.Space(5);

            // 기여값 (코어 + 파츠 합산)
            EditorGUILayout.LabelField("기여값", EditorStyles.miniLabel);
            DrawAssembledStatsSummary();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>장착된 파츠만 우측 요약에 표시</summary>
        private void DrawEquippedPartsList(PartInfo part, int depth, string parentKey = "")
        {
            if (part == null || part.childSlots == null) return;

            for (int i = 0; i < part.childSlots.Length; i++)
            {
                var slot = part.childSlots[i];
                string key = GetSlotKey(slot, i, parentKey);
                PartInfo ep = null;
                equipped.TryGetValue(key, out ep);

                // 장착된 슬롯만 표시
                if (ep == null) continue;

                string slotName = GetSlotDisplayLabel(slot, i);
                string partName = ep.name;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(depth * 10f);

                // 슬롯명 → 파츠명
                GUILayout.Label(slotName, EditorStyles.miniLabel, GUILayout.Width(60));
                var prevColor = GUI.color;
                GUI.color = IncreaseColor;
                GUILayout.Label("→ " + partName, EditorStyles.miniLabel);
                GUI.color = prevColor;

                GUILayout.FlexibleSpace();

                // ✕ 제거
                if (GUILayout.Button("✕", EditorStyles.miniButton,
                    GUILayout.Width(18), GUILayout.Height(15)))
                {
                    RemovePart(key);
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();

                // 하위 재귀
                if (ep.childSlots != null && ep.childSlots.Length > 0)
                    DrawEquippedPartsList(ep, depth + 1, key);
            }

            // 장착된 파츠가 없으면 안내
            if (depth == 0 && equipped.Count == 0)
            {
                GUILayout.Label("(장착된 파츠 없음)", EditorStyles.miniLabel);
            }
        }

        // ================================================================
        // 조립 로직
        // ================================================================

        private void SelectEquipmentProject(EquipmentInfo equipment)
        {
            // 전환 확인
            if (asmIsDirty && selectedEquipment != null && selectedEquipment != equipment)
            {
                string curName = !string.IsNullOrEmpty(selectedEquipment.equipmentInfo.displayName)
                    ? selectedEquipment.equipmentInfo.displayName : selectedEquipment.name;

                int result = EditorUtility.DisplayDialogComplex(
                    "변경사항 저장",
                    $"{curName}에 저장되지 않은 조립 변경사항이 있습니다.",
                    "저장", "저장 안 함", "취소");

                if (result == 0) SaveAssemblyState();
                else if (result == 1) RevertAssemblyState();
                else return;
            }

            selectedEquipment = equipment;
            selectedCore = null;
            equipped.Clear();
            selectedSlotId = null;

            treeCollapsed.Clear();
            needRecompute = true;
            ClearPreviewInstances();

            // 저장된 조립 상태 복원
            LoadAssemblyState();

            // 스냅샷
            asmSnapshot = equipment != null ? JsonUtility.ToJson(equipment) : null;
            asmIsDirty = false;

            string name = !string.IsNullOrEmpty(equipment.equipmentInfo.displayName)
                ? equipment.equipmentInfo.displayName : equipment.name;
            if (selectedCore == null)
                outputSOName = name;
            AddLog($"[장비] {name}");

            if (selectedCore != null)
                RebuildAssemblyPreview();
        }

        /// <summary>
        /// .equipment 확장자 파일인지 확인
        /// </summary>
        private bool IsEquipmentExtension(EquipmentInfo info)
        {
            if (info == null) return false;
            string path = AssetDatabase.GetAssetPath(info);
            if (string.IsNullOrEmpty(path)) return false;
            return System.IO.Path.GetExtension(path).ToLower() == ".equipment";
        }

        /// <summary>
        /// .part 확장자 파일인지 확인
        /// </summary>
        private bool IsPartExtension(PartInfo info)
        {
            if (info == null) return false;
            string path = AssetDatabase.GetAssetPath(info);
            if (string.IsNullOrEmpty(path)) return false;
            return System.IO.Path.GetExtension(path).ToLower() == ".part";
        }

        private Vector2 coreSelectionScroll;
        private string coreSearchFilter = "";

        /// <summary>
        /// 코어 파츠 선택 단계 UI
        /// </summary>
        private void DrawCoreSelectionStep()
        {
            coreSearchFilter = EditorGUILayout.TextField(coreSearchFilter,
                EditorStyles.toolbarSearchField);

            EditorGUILayout.Space(3);
            for (int i = 0; i < partDataList.Count; i++)
            {
                var data = partDataList[i];
                if (data == null) continue;
                if (!IsCoreAsset(data)) continue;
                if (!MatchesSearch(data, coreSearchFilter)) continue;

                string displayName = data.name;
                int slotCount = data.childSlots != null ? data.childSlots.Length : 0;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                var c = GUI.color;
                GUI.color = CoreBadgeColor;
                GUILayout.Label("코어", EditorStyles.miniButton, GUILayout.Width(30));
                GUI.color = c;

                GUILayout.Label(displayName, EditorStyles.label);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);
                GUILayout.Label($"{data.weight}kg", EditorStyles.miniLabel, GUILayout.Width(40));
                GUILayout.Label($"{slotCount}슬롯", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // 카드 전체 영역 클릭
                Rect coreCardRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && coreCardRect.Contains(Event.current.mousePosition))
                {
                    SelectCore(data);
                    Event.current.Use();
                }
            }
        }

        private void SelectCore(PartInfo core)
        {
            selectedCore = core;
            equipped.Clear();
            selectedSlotId = null;

            treeCollapsed.Clear();
            needRecompute = true;

            string name = core.name;
            outputSOName = name + "_조립";
            AddLog($"[코어] {name}");
            RebuildAssemblyPreview();
        }

        private bool IsCoreAsset(PartInfo info)
        {
            if (info == null || info.partTags == null) return false;
            for (int i = 0; i < info.partTags.Length; i++)
                if (info.partTags[i] == PartTagPresets.Frame) return true;
            return false;
        }

        private void EquipPart(string slotId, PartInfo part)
        {
            if (part == null || selectedCore == null) return;

            PartSlot slot;
            if (!FindSlotInTree(selectedCore, slotId, out slot)) return;

            // 태그 호환성
            if (!AssemblyAssembler.IsCompatible(slot, part))
            {
                AddLog($"[실패] 태그 불일치 → {slotId}");
                return;
            }

            equipped[slotId] = part;
            needRecompute = true;
            asmIsDirty = true;

            AddLog($"[장착] {part.name} → {slotId}");
            RebuildAssemblyPreview(false);
        }

        private void RemovePart(string slotId)
        {
            if (!equipped.ContainsKey(slotId)) return;

            string partName = "";
            var removed = equipped[slotId];
            if (removed != null)
            {
                partName = removed.name;

                // 하위 슬롯의 장착도 제거
                if (removed.childSlots != null)
                {
                    for (int i = 0; i < removed.childSlots.Length; i++)
                        RemovePartRecursive(GetSlotKey(removed.childSlots[i], i, slotId));
                }
            }

            equipped.Remove(slotId);
            needRecompute = true;
            asmIsDirty = true;
            if (selectedSlotId == slotId) selectedSlotId = null;
            AddLog($"[제거] {partName}");
            RebuildAssemblyPreview(false);
        }

        private void RemovePartRecursive(string key)
        {
            if (string.IsNullOrEmpty(key) || !equipped.ContainsKey(key)) return;
            var ep = equipped[key];
            if (ep != null && ep.childSlots != null)
            {
                for (int i = 0; i < ep.childSlots.Length; i++)
                    RemovePartRecursive(GetSlotKey(ep.childSlots[i], i, key));
            }
            equipped.Remove(key);
        }

        private bool FindSlotInTree(PartInfo root, string targetKey, out PartSlot result, string parentKey = "")
        {
            result = default;
            if (root == null || root.childSlots == null) return false;

            for (int i = 0; i < root.childSlots.Length; i++)
            {
                string key = GetSlotKey(root.childSlots[i], i, parentKey);
                if (key == targetKey)
                {
                    result = root.childSlots[i];
                    return true;
                }

                PartInfo child;
                if (equipped.TryGetValue(key, out child) && child != null)
                {
                    if (FindSlotInTree(child, targetKey, out result, key))
                        return true;
                }
            }
            return false;
        }

        // ================================================================
        // 워크숍 탭
        // ================================================================

        private void DrawWorkshopTab()
        {
            panelLayout.BeginLeftPanel();
            DrawWorkshopBrowser();
            panelLayout.EndLeftPanel();

            panelLayout.BeginCenterPanel();
            DrawWorkshopEditor();
            panelLayout.EndCenterPanel();

            panelLayout.BeginRightPanel();
            DrawWorkshopSummary();
            panelLayout.EndRightPanel();
        }

        // ── 워크숍: 좌측 파츠 라이브러리 ──

        private void DrawWorkshopBrowser()
        {
            EditorGUILayout.LabelField("파츠 라이브러리", EditorStyles.miniBoldLabel);
            wsSearchFilter = EditorGUILayout.TextField(wsSearchFilter, EditorStyles.toolbarSearchField);

            // CRUD
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("신규", EditorStyles.miniButton)) CreatePartInfo();
            GUI.enabled = mainEquipment != null;
            if (GUILayout.Button("복제", EditorStyles.miniButton)) DuplicatePartInfo();
            if (GUILayout.Button("삭제", EditorStyles.miniButton)) DeletePartInfo();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 목록
            wsBrowserScroll = EditorGUILayout.BeginScrollView(wsBrowserScroll);
            for (int i = 0; i < partDataList.Count; i++)
            {
                var data = partDataList[i];
                if (data == null) continue;
                if (!MatchesSearch(data, wsSearchFilter)) continue;

                bool isSel = mainEquipment == data;
                bool isCore = IsCoreAsset(data);
                bool isPart = IsPartAsset(data);

                string displayName = data.name;

                // 카드
                EditorGUILayout.BeginVertical(isSel ? "SelectionRect" : EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                if (isCore)
                {
                    var c = GUI.color;
                    GUI.color = CoreBadgeColor;
                    GUILayout.Label("코어", EditorStyles.miniButton, GUILayout.Width(30));
                    GUI.color = c;
                }

                GUILayout.Label(displayName, isSel ? EditorStyles.boldLabel : EditorStyles.label);
                EditorGUILayout.EndHorizontal();

                // 태그 미니뱃지 (텍스트 맞춤 너비 + 줄 넘김)
                if (isPart && data.partTags != null && data.partTags.Length > 0)
                {
                    float tagLineW = 5f;
                    float tagMaxW = EditorGUIUtility.currentViewWidth * 0.25f - 10f; // 좌측 패널 폭 기준
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(5);

                    for (int t = 0; t < data.partTags.Length; t++)
                    {
                        string tag = data.partTags[t];
                        float chipW = EditorStyles.miniButton.CalcSize(new GUIContent(tag)).x + 4f;

                        // 줄 넘김
                        if (tagLineW + chipW > tagMaxW && tagLineW > 5f)
                        {
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(5);
                            tagLineW = 5f;
                        }

                        GUILayout.Label(tag, EditorStyles.miniButton, GUILayout.Width(chipW));
                        tagLineW += chipW + 2f;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();

                // 카드 전체 영역 클릭
                Rect wsCardRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && wsCardRect.Contains(Event.current.mousePosition))
                {
                    SelectWorkshopPart(data);
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();

            // 새로고침 + JSON
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("새로고침", EditorStyles.miniButton)) RefreshDataList();
            if (GUILayout.Button("JSON↑", EditorStyles.miniButton, GUILayout.Width(40)))
                ExportToJson();
            if (GUILayout.Button("JSON↓", EditorStyles.miniButton, GUILayout.Width(40)))
                ImportFromJson();
            EditorGUILayout.EndHorizontal();
        }

        private void SelectWorkshopPart(PartInfo part)
        {
            // 전환 확인
            if (wsIsDirty && mainEquipment != null && mainEquipment != part)
            {
                int result = EditorUtility.DisplayDialogComplex(
                    "변경사항 저장",
                    $"{mainEquipment.name}에 저장되지 않은 변경사항이 있습니다.",
                    "저장", "저장 안 함", "취소");

                if (result == 0) SaveWorkshop();
                else if (result == 1) RevertWorkshop();
                else return;
            }

            mainEquipment = part;
            mainSerializedObj = part != null ? new SerializedObject(part) : null;
            wsSelectedSlotIdx = -1;
            GUI.FocusControl(null);

            // 스냅샷
            wsSnapshot = part != null ? JsonUtility.ToJson(part) : null;
            wsIsDirty = false;

            string name = part != null ? part.name : "없음";
            AddLog($"[편집] {name}");
            RebuildWorkshopPreview();
        }

        private void MarkWorkshopDirty()
        {
            if (mainEquipment == null) return;
            EditorUtility.SetDirty(mainEquipment);
            wsIsDirty = true;
        }

        private void SaveWorkshop()
        {
            if (mainEquipment == null) return;
            if (mainSerializedObj != null)
                mainSerializedObj.ApplyModifiedProperties();
            EditorUtility.SetDirty(mainEquipment);
            SaveIfCustomExtension(mainEquipment);
            AssetDatabase.SaveAssets();
            wsSnapshot = JsonUtility.ToJson(mainEquipment);
            wsIsDirty = false;
            AddLog($"[저장] {mainEquipment.name}");
        }

        private void RevertWorkshop()
        {
            if (mainEquipment == null || string.IsNullOrEmpty(wsSnapshot)) return;
            JsonUtility.FromJsonOverwrite(wsSnapshot, mainEquipment);
            mainSerializedObj = new SerializedObject(mainEquipment);
            wsIsDirty = false;
            AddLog($"[원복] {mainEquipment.name}");
            RebuildWorkshopPreview();
        }

        // ── 워크숍: 중앙 편집 ──

        private void DrawWorkshopEditor()
        {
            if (mainEquipment == null)
            {
                EditorGUILayout.HelpBox("← 파츠를 선택하거나 '신규'를 생성하세요.", MessageType.Info);
                return;
            }

            if (mainSerializedObj == null || mainSerializedObj.targetObject != mainEquipment)
                mainSerializedObj = new SerializedObject(mainEquipment);
            mainSerializedObj.Update();

            // 헤더 (스크롤 밖 — 항상 보임)
            DrawWorkshopHeader();
            EditorGUILayout.Space(2);

            wsEditorScroll = EditorGUILayout.BeginScrollView(wsEditorScroll);

            // 기본 정보
            DrawWorkshopBasicInfo();
            EditorGUILayout.Space(5);

            // 비주얼 뷰포트
            if (wsShowVisual = EditorGUILayout.Foldout(wsShowVisual, "비주얼 오브젝트 & 슬롯 배치", true))
            {
                DrawViewport(ViewportMode.Workshop);
                EditorGUILayout.Space(3);
                DrawWorkshopSlotEditor();
            }

            EditorGUILayout.Space(5);

            // 기여값
            DrawWorkshopContribution();

            if (mainSerializedObj.ApplyModifiedProperties())
            {
                SaveIfCustomExtension(mainEquipment);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawWorkshopHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            string name = !string.IsNullOrEmpty(mainEquipment.name)
                ? mainEquipment.name : mainEquipment.name;

            if (IsCoreAsset(mainEquipment))
            {
                var c = GUI.color;
                GUI.color = CoreBadgeColor;
                GUILayout.Label("코어", EditorStyles.miniButton, GUILayout.Width(30));
                GUI.color = c;
            }

            string dirtyMark = wsIsDirty ? " *" : "";
            GUILayout.Label(name + dirtyMark, EditorStyles.boldLabel);
            GUILayout.Label(mainEquipment.name, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            GUI.enabled = wsIsDirty;
            if (GUILayout.Button("저장", EditorStyles.miniButton, GUILayout.Width(35)))
                SaveWorkshop();
            if (GUILayout.Button("원복", EditorStyles.miniButton, GUILayout.Width(35)))
                RevertWorkshop();
            GUI.enabled = true;
            if (GUILayout.Button("이름변경", EditorStyles.miniButton, GUILayout.Width(50)))
                RenameEquipmentInfo();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawWorkshopBasicInfo()
        {
            if (!(wsShowBasic = EditorGUILayout.Foldout(wsShowBasic, "기본 정보", true))) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ScriptedImporter 에셋은 PropertyField가 읽기 전용이므로 직접 그린다
            EditorGUI.BeginChangeCheck();

            GUI.enabled = false;
            EditorGUILayout.TextField("Name", mainEquipment.name);
            GUI.enabled = true;

            // Icon(좌) + SizeType(우)
            EditorGUILayout.BeginHorizontal();
            mainEquipment.icon = (Sprite)EditorGUILayout.ObjectField(
                mainEquipment.icon, typeof(Sprite), false, GUILayout.Width(64), GUILayout.Height(64));

            GUILayout.Space(32);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            mainEquipment.weight = EditorGUILayout.FloatField("Weight", mainEquipment.weight);

            if (EditorGUI.EndChangeCheck())
            {
                MarkWorkshopDirty();
            }

            // visualPrefab (partContribution 소속이지만 기본정보에 노출)
            if (mainEquipment.contribution != null)
            {
                // Renderer 타입 필드 + 프리팹 피커 버튼
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var newRenderer = (Renderer)EditorGUILayout.ObjectField(
                    "Visual Prefab", mainEquipment.contribution.visualPrefab,
                    typeof(Renderer), false);
                if (EditorGUI.EndChangeCheck())
                {
                    mainEquipment.contribution.visualPrefab = newRenderer;
                    MarkWorkshopDirty();
                    RebuildWorkshopPreview();
                }
                if (GUILayout.Button("◎", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    visualPickerTarget = VisualPickerTarget.Workshop;
                    visualPrefabPickerControlId = GUIUtility.GetControlID(FocusType.Passive);
                    var curGo = mainEquipment.contribution.visualPrefab != null
                        ? mainEquipment.contribution.visualPrefab.gameObject : null;
                    EditorGUIUtility.ShowObjectPicker<GameObject>(curGo, false, "t:Prefab", visualPrefabPickerControlId);
                }
                EditorGUILayout.EndHorizontal();

                // 비주얼 오프셋
                EditorGUI.BeginChangeCheck();
                var newOffset = EditorGUILayout.Vector3Field("Visual Offset",
                    mainEquipment.contribution.visualOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    mainEquipment.contribution.visualOffset = newOffset;
                    MarkWorkshopDirty();
                }

                // 비주얼 회전 오프셋
                EditorGUI.BeginChangeCheck();
                var newRotOffset = EditorGUILayout.Vector3Field("Visual Rotation",
                    mainEquipment.contribution.visualRotationOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    mainEquipment.contribution.visualRotationOffset = newRotOffset;
                    MarkWorkshopDirty();
                }
            }

            // 파츠 태그
            EditorGUILayout.Space(4);
            var lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f, 1f));
            EditorGUILayout.Space(4);
            DrawPartTagDropdown();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 파츠 태그를 칩 + [+] 드롭다운 방식으로 그린다.
        /// </summary>
        private void DrawPartTagDropdown()
        {
            DrawTagChipsWithDropdown(
                "파츠 태그",
                mainEquipment.partTags,
                newTags =>
                {
                    mainEquipment.partTags = newTags;
                    MarkWorkshopDirty();
                });
        }

        /// <summary>
        /// string[] 태그를 칩 나열 + [+] 드롭다운으로 그리는 재사용 메서드.
        /// 선택된 태그는 [tag ✕] 칩으로 표시, +버튼 → GenericMenu로 추가.
        /// </summary>
        private void DrawTagChipsWithDropdown(string label, string[] currentTags, System.Action<string[]> onChanged)
        {
            if (currentTags == null) currentTags = new string[0];

            // 라벨 + 태그 칩들 + [+] 버튼을 한 흐름으로
            EditorGUILayout.BeginHorizontal();
            float labelW = 0f;
            if (!string.IsNullOrEmpty(label))
            {
                GUILayout.Space(0);
                var labelRect = GUILayoutUtility.GetRect(60, EditorGUIUtility.singleLineHeight, EditorStyles.miniBoldLabel, GUILayout.Width(60));
                labelRect.y -= 5f;
                GUI.Label(labelRect, label, EditorStyles.miniBoldLabel);
                labelW = 60f;
            }

            float lineWidth = labelW;
            float maxWidth = EditorGUIUtility.currentViewWidth - 40f;
            float plusBtnWidth = 24f;

            for (int i = 0; i < currentTags.Length; i++)
            {
                string tag = currentTags[i];
                string chipText = $"{tag} ✕";
                float chipWidth = EditorStyles.miniButton.CalcSize(new GUIContent(chipText)).x + 4f;

                // 줄 넘김 (+ 버튼 폭도 고려)
                if (lineWidth + chipWidth + plusBtnWidth > maxWidth && lineWidth > labelW)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(labelW);
                    lineWidth = labelW;
                }

                if (GUILayout.Button(chipText, EditorStyles.miniButton, GUILayout.Width(chipWidth)))
                {
                    var list = new List<string>(currentTags);
                    list.RemoveAt(i);
                    onChanged(list.ToArray());
                    GUIUtility.ExitGUI();
                    return;
                }
                lineWidth += chipWidth + 2f;
            }

            // + 버튼 (태그 칩 바로 오른쪽)
            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                ShowTagDropdownMenu(currentTags, onChanged);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ShowTagDropdownMenu(string[] currentTags, System.Action<string[]> onChanged)
        {
            var menu = new GenericMenu();
            var selectedSet = new HashSet<string>(currentTags);

            // Frame(Core) — 최상단 단독 표시
            if (selectedSet.Contains(PartTagPresets.Frame))
            {
                menu.AddDisabledItem(new GUIContent("Frame(Core) ✓"));
            }
            else
            {
                menu.AddItem(new GUIContent("Frame(Core)"), false, () =>
                {
                    var list = new List<string>(currentTags);
                    list.Add(PartTagPresets.Frame);
                    onChanged(list.ToArray());
                });
            }

            menu.AddSeparator("");

            // 카테고리별 태그
            var categories = PartTagPresets.Categories;
            for (int c = 0; c < categories.Length; c++)
            {
                var cat = categories[c];
                for (int t = 0; t < cat.tags.Length; t++)
                {
                    string tag = cat.tags[t];
                    if (selectedSet.Contains(tag))
                    {
                        menu.AddDisabledItem(new GUIContent($"{cat.categoryName}/{tag} ✓"));
                    }
                    else
                    {
                        string capturedTag = tag;
                        menu.AddItem(new GUIContent($"{cat.categoryName}/{tag}"), false, () =>
                        {
                            var list = new List<string>(currentTags);
                            list.Add(capturedTag);
                            onChanged(list.ToArray());
                        });
                    }
                }
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("커스텀 태그 입력..."), false, () =>
            {
                string input = EditorInputDialog.Show("커스텀 태그", "태그 이름:", "");
                if (!string.IsNullOrWhiteSpace(input))
                {
                    string newTag = input.Trim().ToLower().Replace(" ", "_");
                    if (!selectedSet.Contains(newTag))
                    {
                        var list = new List<string>(currentTags);
                        list.Add(newTag);
                        onChanged(list.ToArray());
                    }
                }
            });

            menu.ShowAsContext();
        }

        /// <summary>
        /// 단일 태그 드롭다운. connectToTag 같은 string 필드용.
        /// 선택된 태그가 있으면 [tag ✕] 칩 표시, 없으면 [선택] 버튼.
        /// </summary>
        private void DrawSingleTagDropdown(string label, string currentTag, System.Action<string> onChanged)
        {
            EditorGUILayout.BeginHorizontal();

            if (!string.IsNullOrEmpty(label))
            {
                GUILayout.Space(0);
                var labelRect = GUILayoutUtility.GetRect(60, EditorGUIUtility.singleLineHeight,
                    EditorStyles.miniBoldLabel, GUILayout.Width(60));
                labelRect.y -= 5f;
                GUI.Label(labelRect, label, EditorStyles.miniBoldLabel);
            }

            if (!string.IsNullOrEmpty(currentTag))
            {
                // 선택된 태그 칩 표시
                float chipW = EditorStyles.miniButton.CalcSize(new GUIContent(currentTag + " ✕")).x + 4f;
                if (GUILayout.Button(currentTag + " ✕", EditorStyles.miniButton, GUILayout.Width(chipW)))
                {
                    onChanged(null);
                }
            }

            // [+] 또는 [선택] 버튼
            string btnLabel = string.IsNullOrEmpty(currentTag) ? "선택" : "변경";
            if (GUILayout.Button(btnLabel, EditorStyles.miniButton, GUILayout.Width(40)))
            {
                ShowSingleTagMenu(currentTag, onChanged);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ShowSingleTagMenu(string currentTag, System.Action<string> onChanged)
        {
            var menu = new GenericMenu();

            // 없음 옵션
            menu.AddItem(new GUIContent("(없음)"), string.IsNullOrEmpty(currentTag), () =>
            {
                onChanged(null);
            });

            menu.AddSeparator("");

            // Frame(Core)
            bool isFrame = currentTag == PartTagPresets.Frame;
            menu.AddItem(new GUIContent("Frame(Core)"), isFrame, () =>
            {
                onChanged(PartTagPresets.Frame);
            });

            menu.AddSeparator("");

            // 카테고리별
            var categories = PartTagPresets.Categories;
            for (int c = 0; c < categories.Length; c++)
            {
                var cat = categories[c];
                for (int t = 0; t < cat.tags.Length; t++)
                {
                    string tag = cat.tags[t];
                    string capturedTag = tag;
                    bool selected = tag == currentTag;
                    menu.AddItem(new GUIContent($"{cat.categoryName}/{tag}"), selected, () =>
                    {
                        onChanged(capturedTag);
                    });
                }
            }

            menu.ShowAsContext();
        }

        private void DrawWorkshopSlotEditor()
        {
            if (!(wsShowSlots = EditorGUILayout.Foldout(wsShowSlots, "파츠 슬롯 목록", true))) return;

            var slots = mainEquipment.childSlots;
            if (slots == null) slots = new PartSlot[0];

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                bool isOpen = wsSelectedSlotIdx == i;

                // 헤더 버튼: [▶ 필수 | tag tag tag | ✕]
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // 폴드 토글 (▶/▼ + 필수)
                string arrow = isOpen ? "▼" : "▶";
                string reqMark = slot.isRequired ? "● " : "";
                var tags = slot.acceptedTags ?? new string[0];
                string tagStr = tags.Length > 0 ? string.Join(", ", tags) : "(태그 없음)";
                string btnLabel = $"{arrow} {reqMark}{tagStr}";

                var slotBtnStyle = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };
                if (GUILayout.Button(btnLabel, slotBtnStyle, GUILayout.ExpandWidth(true)))
                    wsSelectedSlotIdx = isOpen ? -1 : i;

                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    var list = new List<PartSlot>(mainEquipment.childSlots);
                    list.RemoveAt(i);
                    mainEquipment.childSlots = list.ToArray();
                    if (wsSelectedSlotIdx == i) wsSelectedSlotIdx = -1;
                    else if (wsSelectedSlotIdx > i) wsSelectedSlotIdx--;
                    MarkWorkshopDirty();
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();

                // 펼침 영역
                if (isOpen)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginChangeCheck();
                    slot.isRequired = EditorGUILayout.Toggle("필수", slot.isRequired);
                    if (EditorGUI.EndChangeCheck())
                    {
                        mainEquipment.childSlots[i] = slot;
                        MarkWorkshopDirty();
                    }

                    // 허용 태그 - 칩 + 드롭다운
                    int slotIdx = i;
                    EditorGUILayout.LabelField("허용 태그", EditorStyles.boldLabel);
                    DrawTagChipsWithDropdown(null, mainEquipment.childSlots[slotIdx].acceptedTags, newTags =>
                    {
                        var s = mainEquipment.childSlots[slotIdx];
                        s.acceptedTags = newTags;
                        mainEquipment.childSlots[slotIdx] = s;
                        MarkWorkshopDirty();
                    });

                    EditorGUI.BeginChangeCheck();
                    slot.localPosition = EditorGUILayout.Vector3Field("위치", slot.localPosition);
                    slot.localRotation = EditorGUILayout.Vector3Field("회전", slot.localRotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        mainEquipment.childSlots[i] = slot;
                        MarkWorkshopDirty();
                    }

                    int capturedSlotIdx = i;
                    EditorGUILayout.LabelField("연결 대상", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    DrawSingleTagDropdown("태그", slot.connectToTag, newTag =>
                    {
                        var s = mainEquipment.childSlots[capturedSlotIdx];
                        s.connectToTag = newTag;
                        mainEquipment.childSlots[capturedSlotIdx] = s;
                        MarkWorkshopDirty();
                    });

                    var newConnectVisual = (PartConnectorVisual)EditorGUILayout.ObjectField(
                        "프리팹", slot.connectVisualPrefab, typeof(PartConnectorVisual), false);
                    if (newConnectVisual != slot.connectVisualPrefab)
                    {
                        var s = mainEquipment.childSlots[capturedSlotIdx];
                        s.connectVisualPrefab = newConnectVisual;
                        mainEquipment.childSlots[capturedSlotIdx] = s;
                        MarkWorkshopDirty();
                    }

                    EditorGUI.indentLevel--;

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
            }

            if (GUILayout.Button("+ 슬롯 추가", EditorStyles.miniButton))
            {
                var list = new List<PartSlot>(mainEquipment.childSlots ?? new PartSlot[0]);
                list.Add(new PartSlot
                {
                    isRequired = false,
                    acceptedTags = new string[0]
                });
                mainEquipment.childSlots = list.ToArray();
                MarkWorkshopDirty();
            }
        }

        private void DrawWorkshopContribution()
        {
            if (!(wsShowContrib = EditorGUILayout.Foldout(wsShowContrib, "파츠 기여", true))) return;

            var contribProp = mainSerializedObj.FindProperty("contribution");
            if (contribProp == null)
            {
                EditorGUILayout.HelpBox("contribution이 null입니다.", MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;

            DrawToggle(contribProp, "hasDefense", null, "방어 기여");
            if (contribProp.FindPropertyRelative("hasDefense").boolValue)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("defenseValue"));
                EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("hardness"));
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
            DrawToggle(contribProp, "hasContainer", null, "컨테이너 기여");
            if (contribProp.FindPropertyRelative("hasContainer").boolValue)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("containerSlotCount"));
                EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("containerMaxWeight"));
                EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("allowedCategories"), true);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("jamProbability"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("비주얼", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            // Renderer 타입 필드 + 프리팹 피커 버튼
            var visualProp = contribProp.FindPropertyRelative("visualPrefab");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(visualProp);
            if (GUILayout.Button("◎", GUILayout.Width(22), GUILayout.Height(18)))
            {
                visualPickerTarget = VisualPickerTarget.Contrib;
                visualPickerContribProp = visualProp;
                visualPrefabPickerControlId = GUIUtility.GetControlID(FocusType.Passive);
                var curRenderer = visualProp.objectReferenceValue as Renderer;
                var curGo = curRenderer != null ? curRenderer.gameObject : null;
                EditorGUIUtility.ShowObjectPicker<GameObject>(curGo, false, "t:Prefab", visualPrefabPickerControlId);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("variants"), true);
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(contribProp.FindPropertyRelative("sounds"), true);

            EditorGUI.indentLevel--;
        }

        // ── 워크숍: 우측 요약 ──

        private void DrawWorkshopSummary()
        {
            EditorGUILayout.LabelField("요약", EditorStyles.miniBoldLabel);

            if (mainEquipment == null)
            {
                EditorGUILayout.HelpBox("파츠를 선택하세요.", MessageType.Info);
                return;
            }

            wsSummaryScroll = EditorGUILayout.BeginScrollView(wsSummaryScroll);

            // 이름/ID
            string name = !string.IsNullOrEmpty(mainEquipment.name)
                ? mainEquipment.name : mainEquipment.name;
            GUILayout.Label(name, EditorStyles.boldLabel);
            GUILayout.Label(mainEquipment.name, EditorStyles.miniLabel);
            GUILayout.Label($"{mainEquipment.weight} kg",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // 파츠 태그 (텍스트 맞춤 + 줄 넘김)
            EditorGUILayout.LabelField("파츠 태그", EditorStyles.miniLabel);
            if (mainEquipment.partTags != null
                && mainEquipment.partTags.Length > 0)
            {
                float sTagLineW = 5f;
                float sTagMaxW = EditorGUIUtility.currentViewWidth * 0.2f - 10f;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);
                for (int i = 0; i < mainEquipment.partTags.Length; i++)
                {
                    string tag = mainEquipment.partTags[i];
                    float chipW = EditorStyles.miniButton.CalcSize(new GUIContent(tag)).x + 4f;
                    if (sTagLineW + chipW > sTagMaxW && sTagLineW > 5f)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(5);
                        sTagLineW = 5f;
                    }
                    GUILayout.Label(tag, EditorStyles.miniButton, GUILayout.Width(chipW));
                    sTagLineW += chipW + 2f;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            // 슬롯 배치
            EditorGUILayout.LabelField("슬롯 배치", EditorStyles.miniLabel);
            if (mainEquipment.childSlots != null)
            {
                for (int i = 0; i < mainEquipment.childSlots.Length; i++)
                {
                    var sl = mainEquipment.childSlots[i];
                    float slotMaxW = EditorGUIUtility.currentViewWidth * 0.2f - 10f;

                    // ●/○ 필수마크(12) + 슬롯명(가변) + 태그들 → 같은 줄, 넘치면 줄바꿈
                    float markW = 14f;
                    float nameW = EditorStyles.miniBoldLabel.CalcSize(new GUIContent(sl.slotId)).x + 4f;
                    float lineW = markW + nameW;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    // 필수 표시 (맨 앞)
                    var prevColor = GUI.color;
                    if (sl.isRequired)
                    {
                        GUI.color = RequiredColor;
                        GUILayout.Label("●", EditorStyles.miniLabel, GUILayout.Width(12));
                    }
                    else
                    {
                        GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        GUILayout.Label("○", EditorStyles.miniLabel, GUILayout.Width(12));
                    }
                    GUI.color = prevColor;

                    // 슬롯 이름
                    GUILayout.Label(sl.slotId, EditorStyles.miniBoldLabel, GUILayout.Width(nameW));

                    // 허용 태그 (같은 줄에 이어서, 넘치면 줄바꿈)
                    if (sl.acceptedTags != null && sl.acceptedTags.Length > 0)
                    {
                        for (int t = 0; t < sl.acceptedTags.Length; t++)
                        {
                            string atTag = sl.acceptedTags[t];
                            float atW = EditorStyles.miniButton.CalcSize(new GUIContent(atTag)).x + 4f;
                            if (lineW + atW > slotMaxW && lineW > markW + nameW)
                            {
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(markW + nameW);
                                lineW = markW + nameW;
                            }
                            GUILayout.Label(atTag, EditorStyles.miniButton, GUILayout.Width(atW));
                            lineW += atW + 2f;
                        }
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.Space(5);

            // 기여값
            EditorGUILayout.LabelField("기여값", EditorStyles.miniLabel);
            if (mainEquipment.contribution != null)
                DrawPartContributionStats(mainEquipment.contribution);

            EditorGUILayout.EndScrollView();
        }

        private void DrawPartContributionStats(PartContribution pc)
        {
            if (pc.hasDefense)
            {
                GUILayout.Label("  방어", EditorStyles.miniBoldLabel);
                DrawContribRow("  방어력", pc.defenseValue);
                DrawContribRow("  경도", pc.hardness);
            }
            if (pc.hasContainer)
            {
                GUILayout.Label("  컨테이너", EditorStyles.miniBoldLabel);
                DrawContribRow("  슬롯", pc.containerSlotCount);
                DrawContribRow("  무게", pc.containerMaxWeight, "F1");
            }
            if (pc.jamProbability != 0)
                DrawContribRow("  잼확률", pc.jamProbability);
        }

        /// <summary>조립 요약. 전투 스탯 미리보기는 게임 전투 시스템 의존이라 제거됨
        /// (EquipmentAssemblyKit). 스탯 합산은 partModifiers(EquipmentAssembler.CollectPartModifiers) 참조.</summary>
        private void DrawAssembledStatsSummary()
        {
            if (selectedCore == null) return;
            // 게임별 전투/방어 스탯 비교 표시는 받는 게임의 전투 데이터에 맞춰 확장한다.
        }

        // ================================================================
        // 스탯 합산
        // ================================================================

        private void RecomputeStats()
        {
            if (selectedEquipment == null || selectedCore == null) return;

            PartData[] tempParts = BuildTempPartsFromCore();
            partModifiers = EquipmentAssembler.CollectPartModifiers(tempParts);
        }

        private PartData[] BuildTempPartsFromCore()
        {
            if (selectedCore == null || selectedCore.childSlots == null) return null;

            var parts = new PartData[selectedCore.childSlots.Length];
            for (int i = 0; i < selectedCore.childSlots.Length; i++)
            {
                string key = GetSlotKey(selectedCore.childSlots[i], i);
                PartInfo ep;
                if (equipped.TryGetValue(key, out ep) && ep != null)
                    parts[i] = new PartData(ep, i);
            }
            return parts;
        }

        // ================================================================
        // Status Bar & 키보드
        // ================================================================

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (!string.IsNullOrEmpty(lastLogMessage))
                EditorGUILayout.LabelField(lastLogMessage, EditorStyles.miniLabel,
                    GUILayout.MaxWidth(position.width * 0.5f));
            else
                EditorGUILayout.LabelField("준비", EditorStyles.miniLabel, GUILayout.Width(40));

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(System.DateTime.Now.ToString("HH:mm:ss"),
                EditorStyles.miniLabel, GUILayout.Width(55));

            EditorGUILayout.EndHorizontal();
        }

        // 프리팹 피커에서 GameObject 선택 → Renderer 추출하여 할당
        private void HandleVisualPrefabPicker()
        {
            if (Event.current.commandName == "ObjectSelectorUpdated"
                && EditorGUIUtility.GetObjectPickerControlID() == visualPrefabPickerControlId)
            {
                var pickedGo = EditorGUIUtility.GetObjectPickerObject() as GameObject;
                if (pickedGo == null) return;

                var r = pickedGo.GetComponent<Renderer>();
                if (r == null)
                {
                    Debug.LogWarning($"'{pickedGo.name}'에 Renderer 컴포넌트가 없습니다.");
                    return;
                }

                if (visualPickerTarget == VisualPickerTarget.Workshop && mainEquipment != null && mainEquipment.contribution != null)
                {
                    mainEquipment.contribution.visualPrefab = r;
                    MarkWorkshopDirty();
                    RebuildWorkshopPreview();
                }
                else if (visualPickerTarget == VisualPickerTarget.Contrib && visualPickerContribProp != null)
                {
                    visualPickerContribProp.objectReferenceValue = r;
                    visualPickerContribProp.serializedObject.ApplyModifiedProperties();
                }
                Repaint();
            }

            if (Event.current.commandName == "ObjectSelectorClosed"
                && EditorGUIUtility.GetObjectPickerControlID() == visualPrefabPickerControlId)
            {
                var pickedGo = EditorGUIUtility.GetObjectPickerObject() as GameObject;
                if (pickedGo == null)
                {
                    // null 선택 시 (Clear)
                    if (visualPickerTarget == VisualPickerTarget.Workshop && mainEquipment != null && mainEquipment.contribution != null)
                    {
                        mainEquipment.contribution.visualPrefab = null;
                        MarkWorkshopDirty();
                        RebuildWorkshopPreview();
                    }
                    else if (visualPickerTarget == VisualPickerTarget.Contrib && visualPickerContribProp != null)
                    {
                        visualPickerContribProp.objectReferenceValue = null;
                        visualPickerContribProp.serializedObject.ApplyModifiedProperties();
                    }
                }
                visualPickerContribProp = null;
                Repaint();
            }
        }

        private void HandleKeyboardInput()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (tabSystem.currentTab == MainTab.Workshop)
            {
                if (e.control && e.keyCode == KeyCode.N)
                { CreatePartInfo(); e.Use(); return; }
                if (e.control && e.keyCode == KeyCode.D)
                { DuplicatePartInfo(); e.Use(); return; }
                if (e.keyCode == KeyCode.Delete && mainEquipment != null)
                { DeletePartInfo(); e.Use(); return; }
                return;
            }

            if (tabSystem.currentTab == MainTab.Assembly)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    selectedSlotId = null;
                    e.Use();
                    Repaint();
                    return;
                }

                if (e.keyCode == KeyCode.Delete && !string.IsNullOrEmpty(selectedSlotId)
                    && equipped.ContainsKey(selectedSlotId))
                {
                    RemovePart(selectedSlotId);
                    e.Use();
                    Repaint();
                }
            }
        }

        // ================================================================
        // UI 헬퍼
        // ================================================================

        // DrawResizeHandle → EditorPanelLayout로 이동

        private void DrawToggle(SerializedProperty parent, string toggleName, string dataName, string label)
        {
            var toggleProp = parent.FindPropertyRelative(toggleName);
            if (toggleProp == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            toggleProp.boolValue = EditorGUILayout.ToggleLeft(label, toggleProp.boolValue, EditorStyles.boldLabel);

            if (Event.current.type == EventType.Repaint && toggleProp.boolValue)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(r.x - 3, r.y - 2, 2f, r.height + 4),
                    new Color(0.2f, 0.5f, 0.6f));
            }

            if (toggleProp.boolValue && dataName != null)
            {
                EditorGUI.indentLevel++;
                var dataProp = parent.FindPropertyRelative(dataName);
                if (dataProp != null) EditorGUILayout.PropertyField(dataProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPartSummary(PartContribution pc)
        {
            var parts = new List<string>();
            if (pc.hasDefense) parts.Add($"방어{FormatDiff(pc.defenseValue)}");
            if (pc.hasContainer) parts.Add($"슬롯+{pc.containerSlotCount}");
            if (pc.jamProbability != 0) parts.Add($"잼{FormatDiff(pc.jamProbability)}");

            if (parts.Count > 0)
                GUILayout.Label("  > " + string.Join(", ", parts), EditorStyles.miniLabel);
        }

        private void DrawContribRow(string label, float value, string format = "F0")
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80));
            if (Mathf.Abs(value) > 0.001f)
            {
                var c = GUI.color;
                GUI.color = value > 0 ? IncreaseColor : DecreaseColor;
                string str = value > 0 ? $"+{value.ToString(format)}" : value.ToString(format);
                EditorGUILayout.LabelField(str, EditorStyles.boldLabel);
                GUI.color = c;
            }
            else
            {
                EditorGUILayout.LabelField("0");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawContribRow(string label, int value)
        {
            DrawContribRow(label, (float)value, "F0");
        }

        private string FormatDiff(float val) => val >= 0 ? $"+{val}" : $"{val}";
        private string FormatDiff(int val) => val >= 0 ? $"+{val}" : $"{val}";

        private bool MatchesSearch(EquipmentInfo data, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            string dn = data.equipmentInfo.displayName ?? "";
            string id = data.equipmentInfo.id ?? "";
            return dn.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                || id.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                || data.name.Contains(filter, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesSearch(PartInfo data, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return data.name.Contains(filter, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPartAsset(PartInfo info)
        {
            if (info == null) return false;
            return info.partTags != null && info.partTags.Length > 0;
        }

        // ================================================================
        // CRUD
        // ================================================================

        private void CreateNewEquipmentInfo()
        {
            string defaultFolder = GetSaveFolderAbsolute();
            string absolutePath = EditorUtility.SaveFilePanel(
                "새 장비 생성", defaultFolder, "New_EquipmentInfo", "equipment");
            if (string.IsNullOrEmpty(absolutePath)) return;

            string relativePath = AbsoluteToUnityPath(absolutePath);
            if (relativePath == null)
            {
                EditorUtility.DisplayDialog("오류", "Assets/ 또는 Packages/ 내부 경로만 가능합니다.", "확인");
                return;
            }

            var newAsset = ScriptableObject.CreateInstance<EquipmentInfo>();
            newAsset.equipmentInfo.id = System.IO.Path.GetFileNameWithoutExtension(absolutePath);
            newAsset.equipmentInfo.displayName = newAsset.equipmentInfo.id;

            SaveEquipmentToFile(absolutePath, newAsset);
            DestroyImmediate(newAsset);
            AssetDatabase.Refresh();

            var imported = AssetDatabase.LoadAssetAtPath<EquipmentInfo>(relativePath);
            if (imported != null)
            {
                RefreshDataList();
                SelectEquipmentProject(imported);
                Selection.activeObject = imported;
                AddLog($"새 장비 생성: {imported.equipmentInfo.id}");
            }
            GUIUtility.ExitGUI();
        }

        private void DuplicateEquipmentInfo()
        {
            // Assembly 탭: EquipmentInfo 복제
            if (tabSystem.currentTab != MainTab.Assembly || selectedEquipment == null) return;

            string srcPath = AssetDatabase.GetAssetPath(selectedEquipment);
            string folder = System.IO.Path.GetDirectoryName(srcPath).Replace("\\", "/");
            string srcName = System.IO.Path.GetFileNameWithoutExtension(srcPath);
            string ext = System.IO.Path.GetExtension(srcPath);

            if (ext == ".asset") ext = ".equipment";

            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{srcName}_copy{ext}");
            string absNewPath = UnityPathToAbsolute(newPath);

            SaveEquipmentToFile(absNewPath, selectedEquipment);
            AssetDatabase.Refresh();

            var newAsset = AssetDatabase.LoadAssetAtPath<EquipmentInfo>(newPath);
            if (newAsset != null)
            {
                RefreshDataList();
                SelectEquipmentProject(newAsset);
                Selection.activeObject = newAsset;
                AddLog($"복제: {srcName}");
            }
            GUIUtility.ExitGUI();
        }

        private void DeleteEquipmentInfo()
        {
            if (tabSystem.currentTab == MainTab.Assembly)
            {
                if (selectedEquipment == null) return;
                string path = AssetDatabase.GetAssetPath(selectedEquipment);
                string name = !string.IsNullOrEmpty(selectedEquipment.equipmentInfo.displayName)
                    ? selectedEquipment.equipmentInfo.displayName : selectedEquipment.name;

                if (!EditorUtility.DisplayDialog("삭제",
                    $"'{name}'을(를) 삭제하시겠습니까?\n{path}", "삭제", "취소"))
                    return;

                AddLog($"삭제: {name}");
                selectedEquipment = null;
                selectedCore = null;
                equipped.Clear();
                AssetDatabase.DeleteAsset(path);
            }
            else
            {
                if (mainEquipment == null) return;
                string path = AssetDatabase.GetAssetPath(mainEquipment);
                string name = mainEquipment.name;

                if (!EditorUtility.DisplayDialog("삭제",
                    $"'{name}'을(를) 삭제하시겠습니까?\n{path}", "삭제", "취소"))
                    return;

                AddLog($"삭제: {name}");
                mainEquipment = null;
                mainSerializedObj = null;
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            RefreshDataList();
            GUIUtility.ExitGUI();
        }

        private void EnsurePartsFolderExists()
        {
            if (AssetDatabase.IsValidFolder(PARTS_SAVE_FOLDER)) return;
            string[] segments = PARTS_SAVE_FOLDER.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private void CreatePartInfo()
        {
            EnsurePartsFolderExists();
            string absoluteFolder = UnityPathToAbsolute(PARTS_SAVE_FOLDER);
            if (absoluteFolder == null) absoluteFolder = Application.dataPath + "/Data/EQ/Parts";

            string absolutePath = EditorUtility.SaveFilePanel(
                "새 파츠 생성", absoluteFolder, "New_Part", "part");
            if (string.IsNullOrEmpty(absolutePath)) return;

            string relativePath = AbsoluteToUnityPath(absolutePath);
            if (relativePath == null)
            {
                EditorUtility.DisplayDialog("오류", "Assets/ 또는 Packages/ 내부 경로만 가능합니다.", "확인");
                return;
            }

            var newAsset = ScriptableObject.CreateInstance<PartInfo>();
            newAsset.contribution = new PartContribution();

            // 조립탭에서 슬롯 선택 중이면 해당 슬롯의 acceptedTags를 자동 할당
            if (tabSystem.currentTab == MainTab.Assembly && !string.IsNullOrEmpty(selectedSlotId) && selectedCore != null)
            {
                PartSlot sel;
                if (FindSlotInTree(selectedCore, selectedSlotId, out sel)
                    && sel.acceptedTags != null && sel.acceptedTags.Length > 0)
                {
                    newAsset.partTags = (string[])sel.acceptedTags.Clone();
                }
                else
                {
                    newAsset.partTags = new string[0];
                }
            }
            else
            {
                newAsset.partTags = new string[0];
            }

            SaveEquipmentToFile(absolutePath, newAsset);
            DestroyImmediate(newAsset);
            AssetDatabase.Refresh();

            var imported = AssetDatabase.LoadAssetAtPath<PartInfo>(relativePath);
            if (imported != null)
            {
                RefreshDataList();
                SelectWorkshopPart(imported);
                Selection.activeObject = imported;
                tabSystem.currentTab = MainTab.Workshop;
                AddLog($"새 파츠 생성: {imported.name}");
            }
            GUIUtility.ExitGUI();
        }

        private void DuplicatePartInfo()
        {
            if (mainEquipment == null) return;

            string srcPath = AssetDatabase.GetAssetPath(mainEquipment);
            string srcName = System.IO.Path.GetFileNameWithoutExtension(srcPath);

            EnsurePartsFolderExists();
            string newPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{PARTS_SAVE_FOLDER}/{srcName}_copy.part");
            string absNewPath = UnityPathToAbsolute(newPath);

            SaveEquipmentToFile(absNewPath, mainEquipment);
            AssetDatabase.Refresh();

            var newAsset = AssetDatabase.LoadAssetAtPath<PartInfo>(newPath);
            if (newAsset != null)
            {
                RefreshDataList();
                SelectWorkshopPart(newAsset);
                Selection.activeObject = newAsset;
                AddLog($"파츠 복제: {srcName}");
            }
            GUIUtility.ExitGUI();
        }

        private void DeletePartInfo()
        {
            if (mainEquipment == null) return;

            string path = AssetDatabase.GetAssetPath(mainEquipment);
            string name = mainEquipment.name;
            if (string.IsNullOrEmpty(name)) name = mainEquipment.name;

            if (!EditorUtility.DisplayDialog("파츠 삭제",
                $"'{name}'을(를) 삭제하시겠습니까?\n{path}", "삭제", "취소"))
                return;

            AddLog($"파츠 삭제: {name}");
            mainEquipment = null;
            mainSerializedObj = null;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            RefreshDataList();
            GUIUtility.ExitGUI();
        }

        private void ExportToJson()
        {
            ScriptableObject target = tabSystem.currentTab == MainTab.Assembly
                ? (ScriptableObject)selectedEquipment : mainEquipment;
            if (target == null) return;

            string json = EquipmentGuidSerializer.ToJson(target);
            string defaultName = target.name;

            string savePath = EditorUtility.SaveFilePanel("JSON 내보내기", "", defaultName, "json");
            if (string.IsNullOrEmpty(savePath)) return;

            System.IO.File.WriteAllText(savePath, json, System.Text.Encoding.UTF8);
            AddLog($"JSON 내보내기: {defaultName}");
        }

        private void ImportFromJson()
        {
            string loadPath = EditorUtility.OpenFilePanel("JSON 가져오기", "", "json");
            if (string.IsNullOrEmpty(loadPath)) return;

            string json = System.IO.File.ReadAllText(loadPath, System.Text.Encoding.UTF8);
            string defaultFolder = GetSaveFolderAbsolute();
            string fileName = System.IO.Path.GetFileNameWithoutExtension(loadPath);
            string savePath = EditorUtility.SaveFilePanel(
                "가져온 장비 저장 위치", defaultFolder, fileName, "asset");
            if (string.IsNullOrEmpty(savePath)) return;

            string relativePath = AbsoluteToUnityPath(savePath);
            if (relativePath == null)
            {
                EditorUtility.DisplayDialog("오류", "Assets/ 또는 Packages/ 내부 경로만 가능합니다.", "확인");
                return;
            }

            var newAsset = ScriptableObject.CreateInstance<PartInfo>();
            EquipmentGuidSerializer.FromJsonOverwrite(json, newAsset);

            AssetDatabase.CreateAsset(newAsset, relativePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshDataList();
            SelectWorkshopPart(newAsset);
            Selection.activeObject = newAsset;
            AddLog($"JSON 가져오기: {fileName}");
        }

        private void RenameEquipmentInfo()
        {
            if (mainEquipment == null) return;

            string oldPath = AssetDatabase.GetAssetPath(mainEquipment);
            string oldName = System.IO.Path.GetFileNameWithoutExtension(oldPath);
            string ext = System.IO.Path.GetExtension(oldPath);
            string folder = System.IO.Path.GetDirectoryName(oldPath).Replace("\\", "/");

            string newName = EditorInputDialog.Show("이름 변경", "새 파일 이름:", oldName);
            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            // 중복 체크
            string newPath = $"{folder}/{newName}{ext}";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(newPath) != null)
            {
                EditorUtility.DisplayDialog("이름 변경 실패",
                    $"'{newName}{ext}' 파일이 이미 존재합니다.\n다른 이름을 사용하세요.", "확인");
                return;
            }

            string result = AssetDatabase.RenameAsset(oldPath, newName);
            if (!string.IsNullOrEmpty(result))
            {
                Debug.LogError($"[장비 에디터] 이름 변경 실패: {result}");
                return;
            }

            AssetDatabase.SaveAssets();
            RefreshDataList();
            Repaint();
            AddLog($"이름 변경: {oldName} -> {newName}");
        }

        // ================================================================
        // 커스텀 확장자 저장/로드 유틸
        // ================================================================

        /// <summary>
        /// ScriptableObject를 JSON으로 직렬화하여 파일에 저장한다.
        /// </summary>
        private void SaveEquipmentToFile(string absolutePath, ScriptableObject info)
        {
            string json = EquipmentGuidSerializer.ToJson(info);
            string dir = System.IO.Path.GetDirectoryName(absolutePath);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(absolutePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// 커스텀 확장자(.equipment/.part) 파일이면 JSON을 다시 써서 동기화한다.
        /// .asset 파일이면 Unity가 자동 관리하므로 아무것도 안 한다.
        /// </summary>
        private void SaveIfCustomExtension(ScriptableObject info)
        {
            if (info == null) return;
            string assetPath = AssetDatabase.GetAssetPath(info);
            if (string.IsNullOrEmpty(assetPath)) return;

            string ext = System.IO.Path.GetExtension(assetPath).ToLower();
            if (ext != ".equipment" && ext != ".part") return;

            string absPath = UnityPathToAbsolute(assetPath);
            if (absPath == null) return;

            SaveEquipmentToFile(absPath, info);
            AssetDatabase.ImportAsset(assetPath);
        }

        /// <summary>
        /// 커스텀 확장자 파일인지 확인한다.
        /// </summary>
        private bool IsCustomExtensionAsset(ScriptableObject info)
        {
            if (info == null) return false;
            string path = AssetDatabase.GetAssetPath(info);
            if (string.IsNullOrEmpty(path)) return false;
            string ext = System.IO.Path.GetExtension(path).ToLower();
            return ext == ".equipment" || ext == ".part";
        }

        // ================================================================
        // 경로 유틸
        // ================================================================

        private string GetSaveFolderAbsolute()
        {
            ScriptableObject target = mainEquipment != null ? (ScriptableObject)mainEquipment : selectedEquipment;
            if (target != null)
            {
                string unityPath = AssetDatabase.GetAssetPath(target);

                if (unityPath.StartsWith("Packages/"))
                {
                    var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(unityPath);
                    if (pkgInfo != null)
                    {
                        string pkgPrefix = "Packages/" + pkgInfo.name;
                        string relativePart = unityPath.Substring(pkgPrefix.Length);
                        string absolute = pkgInfo.resolvedPath.Replace("\\", "/") + relativePart;
                        string dir = System.IO.Path.GetDirectoryName(absolute).Replace("\\", "/");
                        if (System.IO.Directory.Exists(dir)) return dir;
                    }
                }

                string abs = UnityPathToAbsolute(unityPath);
                if (abs != null)
                {
                    string dir2 = System.IO.Path.GetDirectoryName(abs).Replace("\\", "/");
                    if (System.IO.Directory.Exists(dir2)) return dir2;
                }
            }
            return Application.dataPath;
        }

        private string AbsoluteToUnityPath(string absolutePath)
        {
            absolutePath = absolutePath.Replace("\\", "/");
            string dataPath = Application.dataPath.Replace("\\", "/");
            string projectRoot = System.IO.Path.GetDirectoryName(dataPath).Replace("\\", "/");
            string packagesPath = projectRoot + "/Packages";

            if (absolutePath.StartsWith(dataPath))
                return "Assets" + absolutePath.Substring(dataPath.Length);

            if (absolutePath.StartsWith(packagesPath))
                return "Packages" + absolutePath.Substring(packagesPath.Length);

            var allPackages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            for (int i = 0; i < allPackages.Length; i++)
            {
                string resolved = allPackages[i].resolvedPath.Replace("\\", "/");
                if (absolutePath.StartsWith(resolved))
                    return "Packages/" + allPackages[i].name + absolutePath.Substring(resolved.Length);
            }
            return null;
        }

        private string UnityPathToAbsolute(string unityPath)
        {
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");

            if (unityPath.StartsWith("Assets"))
                return projectRoot + "/" + unityPath;

            if (unityPath.StartsWith("Packages/"))
            {
                var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(unityPath);
                if (pkgInfo != null)
                {
                    string pkgPrefix = "Packages/" + pkgInfo.name;
                    string relativePart = unityPath.Substring(pkgPrefix.Length);
                    return pkgInfo.resolvedPath.Replace("\\", "/") + relativePart;
                }
                return projectRoot + "/" + unityPath;
            }
            return null;
        }
    }
}
