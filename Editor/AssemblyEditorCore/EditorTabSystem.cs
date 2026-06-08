using System;
using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// 제네릭 탭 시스템. Enum 기반으로 탭 바를 그린다.
    /// 활성 탭 하단에 컬러 인디케이터를 표시한다.
    /// </summary>
    public class EditorTabSystem<TEnum> where TEnum : Enum
    {
        public TEnum currentTab;

        /// <summary>각 탭의 표시 이름. null이면 Enum.ToString() 사용.</summary>
        public string[] tabLabels;

        /// <summary>탭 변경 시 호출. (이전 탭, 새 탭)</summary>
        public Action<TEnum, TEnum> onTabChanged;

        public EditorTabSystem(TEnum defaultTab, string[] labels = null)
        {
            currentTab = defaultTab;
            tabLabels = labels;
        }

        /// <summary>
        /// 탭 바를 그린다.
        /// activeColor: 활성 탭 하단 인디케이터 색상.
        /// </summary>
        public void DrawTabBar(Color activeColor)
        {
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            for (int i = 0; i < values.Length; i++)
            {
                bool isActive = currentTab.Equals(values[i]);
                string label = (tabLabels != null && i < tabLabels.Length) ? tabLabels[i] : values[i].ToString();

                bool clicked = GUILayout.Toggle(isActive, label, EditorStyles.toolbarButton);
                if (Event.current.type == EventType.Repaint && isActive)
                {
                    Rect r = GUILayoutUtility.GetLastRect();
                    EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), activeColor);
                }

                if (clicked && !isActive)
                {
                    TEnum prev = currentTab;
                    currentTab = values[i];
                    onTabChanged?.Invoke(prev, currentTab);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}
