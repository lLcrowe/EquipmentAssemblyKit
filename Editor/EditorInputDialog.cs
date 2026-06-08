using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// 간단한 텍스트 입력 팝업 윈도우.
    /// string result = EditorInputDialog.Show("제목", "설명:", "기본값");
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string description;
        private string inputText;
        private string result;
        private bool confirmed;
        private bool firstFrame = true;

        public static string Show(string title, string description, string defaultValue = "")
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.description = description;
            window.inputText = defaultValue;
            window.result = null;
            window.confirmed = false;
            window.minSize = new Vector2(300, 100);
            window.maxSize = new Vector2(500, 100);
            window.ShowModal();
            return window.result;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(description);

            GUI.SetNextControlName("InputField");
            inputText = EditorGUILayout.TextField(inputText);

            if (firstFrame)
            {
                EditorGUI.FocusTextInControl("InputField");
                firstFrame = false;
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("확인", GUILayout.Width(80)) || IsEnterPressed())
            {
                result = inputText;
                confirmed = true;
                Close();
            }

            if (GUILayout.Button("취소", GUILayout.Width(80)))
            {
                result = null;
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool IsEnterPressed()
        {
            var e = Event.current;
            return e.type == EventType.KeyDown
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
        }
    }
}
