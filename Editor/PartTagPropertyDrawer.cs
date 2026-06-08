using lLCroweTool.EquipmentAssemblyKit;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// Tag 선택 에디터 유틸리티.
    /// SerializedProperty(string[])를 카테고리별 체크박스 UI로 그린다.
    /// 장비 태그, 스킬 액션 태그 등 string[] 기반 태그 필드에 범용 사용.
    /// </summary>
    public static class PartTagEditorUtil
    {
        // Foldout 상태 (property path 기준)
        private static Dictionary<string, bool> mainFoldouts = new Dictionary<string, bool>();
        private static Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();
        private static Dictionary<string, bool> customFoldouts = new Dictionary<string, bool>();
        private static Dictionary<string, string> customInputs = new Dictionary<string, string>();

        /// <summary>
        /// 장비 파츠 태그용 (기존 호환). PartTagPresets.Categories 사용.
        /// </summary>
        public static void DrawTagSelector(SerializedProperty arrayProp, string label = null)
        {
            DrawTagSelector(arrayProp, PartTagPresets.Categories, label);
        }

        /// <summary>
        /// 범용 태그 선택기. 카테고리를 외부에서 주입한다.
        /// </summary>
        public static void DrawTagSelector(SerializedProperty arrayProp, TagCategory[] categories, string label = null)
        {
            if (arrayProp == null || !arrayProp.isArray)
            {
                EditorGUILayout.HelpBox("string[] 프로퍼티가 필요합니다.", MessageType.Warning);
                return;
            }

            string key = arrayProp.propertyPath;
            if (!mainFoldouts.ContainsKey(key)) mainFoldouts[key] = false;
            if (!customInputs.ContainsKey(key)) customInputs[key] = "";

            var selectedTags = GetSelectedTags(arrayProp);
            string displayLabel = label ?? arrayProp.displayName;

            // 메인 Foldout
            mainFoldouts[key] = EditorGUILayout.Foldout(
                mainFoldouts[key],
                $"{displayLabel} ({selectedTags.Count}개 선택)",
                true,
                EditorStyles.foldoutHeader
            );

            if (!mainFoldouts[key]) return;

            EditorGUI.indentLevel++;

            if (categories != null)
            {
                for (int i = 0; i < categories.Length; i++)
                {
                    DrawCategory(key, arrayProp, categories[i], i, selectedTags);
                }
            }

            // 커스텀 태그 섹션
            DrawCustomSection(key, arrayProp, selectedTags, categories);

            EditorGUI.indentLevel--;
        }

        private static void DrawCategory(
            string key,
            SerializedProperty arrayProp,
            TagCategory category,
            int catIndex,
            HashSet<string> selectedTags)
        {
            string catKey = key + "_cat_" + catIndex;
            if (!categoryFoldouts.ContainsKey(catKey)) categoryFoldouts[catKey] = false;

            // 카테고리 내 선택 수
            int selected = 0;
            for (int t = 0; t < category.tags.Length; t++)
            {
                if (selectedTags.Contains(category.tags[t])) selected++;
            }

            // 카테고리 헤더
            string catLabel = selected > 0
                ? $"{category.categoryName} ({selected}/{category.tags.Length})"
                : category.categoryName;

            GUIStyle style = selected > 0 ? EditorStyles.foldoutHeader : EditorStyles.foldout;
            categoryFoldouts[catKey] = EditorGUILayout.Foldout(categoryFoldouts[catKey], catLabel, true, style);

            if (!categoryFoldouts[catKey]) return;

            EditorGUI.indentLevel++;

            for (int t = 0; t < category.tags.Length; t++)
            {
                string tag = category.tags[t];
                bool isOn = selectedTags.Contains(tag);
                bool newVal = EditorGUILayout.ToggleLeft(tag, isOn);

                if (newVal != isOn)
                {
                    if (newVal) selectedTags.Add(tag);
                    else selectedTags.Remove(tag);
                    ApplySelectedTags(arrayProp, selectedTags);
                }
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawCustomSection(
            string key,
            SerializedProperty arrayProp,
            HashSet<string> selectedTags,
            TagCategory[] categories = null)
        {
            string customKey = key + "_custom";
            if (!customFoldouts.ContainsKey(customKey)) customFoldouts[customKey] = false;

            var customTags = GetCustomTags(selectedTags, categories);

            string label = customTags.Count > 0
                ? $"커스텀 태그 ({customTags.Count}개)"
                : "커스텀 태그";

            customFoldouts[customKey] = EditorGUILayout.Foldout(customFoldouts[customKey], label, true);

            if (!customFoldouts[customKey]) return;

            EditorGUI.indentLevel++;

            // 기존 커스텀 태그 (제거 가능)
            for (int i = customTags.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ToggleLeft(customTags[i], true);
                if (GUILayout.Button("제거", GUILayout.Width(50)))
                {
                    selectedTags.Remove(customTags[i]);
                    ApplySelectedTags(arrayProp, selectedTags);
                }
                EditorGUILayout.EndHorizontal();
            }

            // 새 태그 입력
            EditorGUILayout.BeginHorizontal();
            customInputs[key] = EditorGUILayout.TextField(customInputs[key]);
            if (GUILayout.Button("추가", GUILayout.Width(50))
                && !string.IsNullOrWhiteSpace(customInputs[key]))
            {
                string newTag = customInputs[key].Trim().ToLower().Replace(" ", "_");
                if (!selectedTags.Contains(newTag))
                {
                    selectedTags.Add(newTag);
                    ApplySelectedTags(arrayProp, selectedTags);
                }
                customInputs[key] = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private static HashSet<string> GetSelectedTags(SerializedProperty arrayProp)
        {
            var tags = new HashSet<string>();
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                string val = arrayProp.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(val)) tags.Add(val);
            }
            return tags;
        }

        private static void ApplySelectedTags(SerializedProperty arrayProp, HashSet<string> tags)
        {
            arrayProp.ClearArray();
            int index = 0;
            foreach (var tag in tags)
            {
                arrayProp.InsertArrayElementAtIndex(index);
                arrayProp.GetArrayElementAtIndex(index).stringValue = tag;
                index++;
            }
            arrayProp.serializedObject.ApplyModifiedProperties();
        }

        private static List<string> GetCustomTags(HashSet<string> selectedTags, TagCategory[] categories = null)
        {
            var presetTags = new HashSet<string>();
            var cats = categories ?? PartTagPresets.Categories;
            if (cats != null)
            {
                for (int i = 0; i < cats.Length; i++)
                {
                    for (int t = 0; t < cats[i].tags.Length; t++)
                    {
                        presetTags.Add(cats[i].tags[t]);
                    }
                }
            }

            var customs = new List<string>();
            foreach (var tag in selectedTags)
            {
                if (!presetTags.Contains(tag)) customs.Add(tag);
            }
            return customs;
        }
    }
}
