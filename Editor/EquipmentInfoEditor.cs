using lLCroweTool.EquipmentAssemblyKit;
using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// EquipmentInfo 커스텀 에디터. 공통 데이터 + 루트 슬롯(태그 드롭다운) 편집.
    /// 카테고리별 데이터(무기/방어구 등)는 partial 확장이라 게임별로 추가한다.
    /// </summary>
    [CustomEditor(typeof(EquipmentInfo))]
    public class EquipmentInfoEditor : Editor
    {
        private SerializedProperty equipmentInfoProp;
        private SerializedProperty rootSlotsProp;
        private bool showRootSlots = true;

        void OnEnable()
        {
            equipmentInfoProp = serializedObject.FindProperty("equipmentInfo");
            rootSlotsProp = serializedObject.FindProperty("rootSlots");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(equipmentInfoProp, true);

            // ── 루트 슬롯 (이 장비가 가진 슬롯) ──
            EditorGUILayout.Space(10);
            showRootSlots = EditorGUILayout.Foldout(showRootSlots, "루트 슬롯", true, EditorStyles.foldoutHeader);
            if (showRootSlots && rootSlotsProp != null)
            {
                EditorGUI.indentLevel++;
                DrawPartSlotsArray(rootSlotsProp);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// PartSlot 배열을 수동으로 그린다.
        /// acceptedTags는 PartTagEditorUtil로 체크박스 UI 표시.
        /// </summary>
        private void DrawPartSlotsArray(SerializedProperty arrayProp)
        {
            // 배열 크기 조절
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("크기", GUILayout.Width(40));
            int newSize = EditorGUILayout.IntField(arrayProp.arraySize, GUILayout.Width(50));
            if (newSize != arrayProp.arraySize)
            {
                arrayProp.arraySize = newSize;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 각 슬롯 원소 그리기
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                var slotIdProp = element.FindPropertyRelative("slotId");
                var acceptedTagsProp = element.FindPropertyRelative("acceptedTags");
                var isRequiredProp = element.FindPropertyRelative("isRequired");

                string slotName = string.IsNullOrEmpty(slotIdProp.stringValue)
                    ? $"슬롯 [{i}]"
                    : $"[{i}] {slotIdProp.stringValue}";

                if (isRequiredProp.boolValue)
                {
                    slotName += " (필수)";
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField(slotName, EditorStyles.boldLabel);

                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(slotIdProp, new GUIContent("슬롯 ID"));
                EditorGUILayout.PropertyField(isRequiredProp, new GUIContent("필수 여부"));

                // acceptedTags → 체크박스 UI
                PartTagEditorUtil.DrawTagSelector(acceptedTagsProp, "허용 태그");

                EditorGUI.indentLevel--;

                // 삭제 버튼
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("슬롯 삭제", GUILayout.Width(80)))
                {
                    arrayProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // 슬롯 추가 버튼
            if (GUILayout.Button("+ 슬롯 추가"))
            {
                arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
            }
        }
    }
}
