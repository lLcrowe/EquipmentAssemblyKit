using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// 3패널 레이아웃 + 가로 리사이즈 핸들.
    /// 좌/중/우 패널 너비를 관리하며, 드래그로 리사이즈한다.
    /// </summary>
    public class EditorPanelLayout
    {
        public float leftPanelWidth = 175f;
        public float rightPanelWidth = 165f;

        public const float MIN_WIDTH = 120f;
        public const float MAX_WIDTH = 400f;
        public const float HANDLE_WIDTH = 6f;

        private bool isResizingLeft;
        private bool isResizingRight;

        /// <summary>Repaint 요청 콜백 (EditorWindow.Repaint() 연결)</summary>
        public System.Action onRepaint;

        // ============================
        // 3패널 구조 헬퍼
        // ============================

        /// <summary>좌측 패널 시작</summary>
        public void BeginLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));
        }

        /// <summary>좌측 패널 종료 + 리사이즈 핸들</summary>
        public void EndLeftPanel()
        {
            EditorGUILayout.EndVertical();
            DrawResizeHandle(ref isResizingLeft, ref leftPanelWidth);
        }

        /// <summary>중앙 패널 시작</summary>
        public void BeginCenterPanel()
        {
            EditorGUILayout.BeginVertical();
        }

        /// <summary>중앙 패널 종료 + 리사이즈 핸들</summary>
        public void EndCenterPanel()
        {
            EditorGUILayout.EndVertical();
            DrawResizeHandle(ref isResizingRight, ref rightPanelWidth);
        }

        /// <summary>우측 패널 시작</summary>
        public void BeginRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));
        }

        /// <summary>우측 패널 종료</summary>
        public void EndRightPanel()
        {
            EditorGUILayout.EndVertical();
        }

        // ============================
        // 리사이즈 핸들
        // ============================

        private void DrawResizeHandle(ref bool resizing, ref float width)
        {
            var handleRect = EditorGUILayout.GetControlRect(
                false, GUILayout.Width(HANDLE_WIDTH), GUILayout.ExpandHeight(true));

            var lineRect = new Rect(
                handleRect.x + (HANDLE_WIDTH - 2f) * 0.5f, handleRect.y,
                2f, handleRect.height);
            EditorGUI.DrawRect(lineRect,
                resizing ? new Color(0.5f, 0.7f, 1f) : new Color(0.3f, 0.3f, 0.3f));

            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (handleRect.Contains(e.mousePosition))
                    { resizing = true; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (resizing)
                    {
                        width = Mathf.Clamp(e.mousePosition.x - handleRect.x + width, MIN_WIDTH, MAX_WIDTH);
                        onRepaint?.Invoke();
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (resizing) { resizing = false; e.Use(); }
                    break;
            }
        }
    }
}
