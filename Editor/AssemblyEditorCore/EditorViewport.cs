using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// PreviewRenderUtility 기반 에디터 뷰포트.
    /// 2D/3D 겸용, 오빗/패닝/줌, GL 마커 렌더, XYZ 축 기즈모.
    /// 장비/차량/로봇 등 조립 에디터에서 재사용한다.
    /// </summary>
    public class EditorViewport : IDisposable
    {
        // ── 상태 ──
        public Vector2 orbitAngle;
        public float zoom = 5f;
        public Vector3 pan;
        public bool is2D;
        public float viewportHeight = 280f;

        // ── 상수 ──
        public const float ZOOM_MIN = 0.5f;
        public const float ZOOM_MAX = 20f;
        public const float VIEWPORT_MIN_HEIGHT = 120f;
        public const float VIEWPORT_MAX_HEIGHT = 600f;
        public const float MARKER_RADIUS = 0.08f;

        // ── 프리뷰 ──
        public PreviewRenderUtility Preview { get; private set; }
        public List<GameObject> Instances { get; private set; } = new List<GameObject>();

        // ── GL 리소스 ──
        public Mesh DiskMesh { get; private set; }
        public Material MarkerMaterial { get; private set; }
        public Material LineMaterial { get; private set; }

        // ── 내부 상태 ──
        private bool isResizingViewport;
        private bool disposed;

        // ── 콜백 ──
        /// <summary>뷰포트 Repaint 요청 시 호출. EditorWindow.Repaint()를 연결한다.</summary>
        public Action onRepaint;

        // ============================
        // 초기화 / 해제
        // ============================

        public void Initialize()
        {
            if (Preview != null) return;
            Preview = new PreviewRenderUtility();
            Preview.camera.fieldOfView = 30f;
            Preview.camera.nearClipPlane = 0.01f;
            Preview.camera.farClipPlane = 100f;
            Preview.camera.clearFlags = CameraClearFlags.SolidColor;
            Preview.camera.backgroundColor = new Color(0.05f, 0.07f, 0.1f);

            Preview.lights[0].intensity = 1.2f;
            Preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            Preview.lights[1].intensity = 0.6f;

            DiskMesh = CreateDiskMesh(24);

            MarkerMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            MarkerMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            MarkerMaterial.SetInt("_ZWrite", 0);

            LineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            LineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            LineMaterial.SetInt("_ZWrite", 0);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            ClearInstances();
            if (Preview != null)
            {
                Preview.Cleanup();
                Preview = null;
            }
            if (DiskMesh != null) UnityEngine.Object.DestroyImmediate(DiskMesh);
            if (MarkerMaterial != null) UnityEngine.Object.DestroyImmediate(MarkerMaterial);
            if (LineMaterial != null) UnityEngine.Object.DestroyImmediate(LineMaterial);
        }

        // ============================
        // 프리뷰 인스턴스 관리
        // ============================

        /// <summary>프리뷰 씬에 오브젝트를 인스턴스화한다.</summary>
        public GameObject Instantiate(GameObject source)
        {
            var instance = UnityEngine.Object.Instantiate(source);
            instance.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(instance, Preview.camera.gameObject.scene);
            Instances.Add(instance);
            return instance;
        }

        public void ClearInstances()
        {
            if (Preview == null) return;
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                if (Instances[i] != null) UnityEngine.Object.DestroyImmediate(Instances[i]);
            }
            Instances.Clear();
        }

        // ============================
        // 자동 프레이밍
        // ============================

        public void AutoFrame()
        {
            if (Instances.Count == 0) return;

            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            for (int i = 0; i < Instances.Count; i++)
            {
                var renderers = Instances[i].GetComponentsInChildren<Renderer>();
                for (int j = 0; j < renderers.Length; j++)
                {
                    if (first) { bounds = renderers[j].bounds; first = false; }
                    else bounds.Encapsulate(renderers[j].bounds);
                }
            }

            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.1f);
            zoom = size * 2.5f;
            pan = Vector3.zero;
            orbitAngle = new Vector2(135f, -25f);
            if (is2D) orbitAngle = Vector2.zero;
        }

        // ============================
        // 카메라 설정
        // ============================

        public void SetupCamera()
        {
            if (is2D)
            {
                Preview.camera.orthographic = true;
                Preview.camera.orthographicSize = zoom * 0.5f;
                Preview.camera.transform.position = new Vector3(pan.x, pan.y, -10f);
                Preview.camera.transform.rotation = Quaternion.identity;
            }
            else
            {
                Preview.camera.orthographic = false;
                Quaternion rot = Quaternion.Euler(-orbitAngle.y, orbitAngle.x, 0f);
                Vector3 camPos = rot * new Vector3(0f, 0f, -zoom) + pan;
                Preview.camera.transform.position = camPos;
                Preview.camera.transform.rotation = rot;
            }
        }

        /// <summary>현재 카메라 회전 Quaternion을 반환한다.</summary>
        public Quaternion GetCameraRotation()
        {
            if (is2D) return Quaternion.identity;
            return Quaternion.Euler(-orbitAngle.y, orbitAngle.x, 0f);
        }

        // ============================
        // 뷰포트 렌더
        // ============================

        /// <summary>
        /// 뷰포트를 그린다.
        /// onRenderGL: GL.PushMatrix 안에서 호출 (3D GL 마커)
        /// onGUIOverlay: EndPreview 후 호출 (2D GUI 라벨)
        /// hasPrefab: false이면 안내 메시지만 표시
        /// </summary>
        public Rect DrawViewport(bool hasPrefab, Action onRenderGL, Action<Rect> onGUIOverlay)
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(viewportHeight));

            if (Preview == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.05f, 0.07f, 0.1f));
                EditorGUI.LabelField(rect, "프리뷰 초기화 실패", EditorStyles.centeredGreyMiniLabel);
                return rect;
            }

            if (!hasPrefab)
            {
                DrawEmptyViewport(rect);
                DrawViewportResizeHandle(rect);
                return rect;
            }

            // 리사이즈 핸들 (HandleViewportInput보다 먼저 처리)
            DrawViewportResizeHandle(rect);

            return rect;
        }

        /// <summary>
        /// 뷰포트 렌더링을 실행한다. DrawViewport에서 rect를 받은 후 호출.
        /// excludeRect: 뷰포트 내 클릭 제외 영역 (Icon 버튼 등)
        /// </summary>
        public void RenderViewport(Rect rect, Action onRenderGL, Action<Rect> onGUIOverlay, Rect excludeRect = default)
        {
            if (Preview == null) return;

            // 마우스 입력 처리 (리사이즈 중 제외)
            if (!isResizingViewport)
                HandleViewportInput(rect, excludeRect);

            // 카메라 설정 + 렌더
            Preview.BeginPreview(rect, GUIStyle.none);
            SetupCamera();
            Preview.camera.Render();

            // URP에서 Camera.Render() 후 RenderTexture.active가 리셋될 수 있음
            // GL 마커가 프리뷰 RT에 그려지도록 명시적 복원
            RenderTexture.active = Preview.camera.targetTexture;

            // GL 마커 오버레이
            GL.PushMatrix();
            GL.LoadProjectionMatrix(Preview.camera.projectionMatrix);
            GL.modelview = Preview.camera.worldToCameraMatrix;

            onRenderGL?.Invoke();

            GL.PopMatrix();

            var resultTex = Preview.EndPreview();
            GUI.DrawTexture(rect, resultTex, ScaleMode.StretchToFill, false);

            // 2D GUI 오버레이
            onGUIOverlay?.Invoke(rect);

            // XYZ 축 기즈모 + 모드 표시
            DrawAxisGizmo(rect);

            var modeLabel = is2D ? "2D" : "3D";
            var modeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperRight,
                normal = { textColor = new Color(1f, 1f, 1f, 0.4f) }
            };
            GUI.Label(new Rect(rect.xMax - 30, rect.y + 2, 26, 14), modeLabel, modeStyle);
        }

        // ============================
        // 스크린샷 캡처
        // ============================

        /// <summary>마커 없이 캡처하여 Texture2D를 반환한다.</summary>
        public Texture2D CaptureStaticPreview(Rect rect)
        {
            if (Preview == null) return null;
            Preview.BeginStaticPreview(rect);
            SetupCamera();
            Preview.camera.Render();
            return Preview.EndStaticPreview();
        }

        // ============================
        // 마우스 입력
        // ============================

        /// <summary>
        /// 뷰포트 기본 입력 처리 (줌, 오빗, 패닝).
        /// 슬롯 히트 테스트 등 추가 입력은 onMouseDown 콜백으로 처리한다.
        /// </summary>
        public Action<Event, Rect> onMouseDown;
        public Action<Event, Rect, Vector2> onMouseDrag;
        public Action<Event> onMouseUp;

        /// <summary>드래그 중 슬롯 인덱스. 외부에서 설정/참조한다.</summary>
        public int dragSlotIdx = -1;

        private void HandleViewportInput(Rect rect, Rect excludeRect = default)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (excludeRect.width > 0 && excludeRect.Contains(e.mousePosition)) return;

            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.type)
            {
                case EventType.ScrollWheel:
                    zoom += e.delta.y * zoom * 0.1f;
                    zoom = Mathf.Clamp(zoom, ZOOM_MIN, ZOOM_MAX);
                    e.Use();
                    onRepaint?.Invoke();
                    break;

                case EventType.MouseDown:
                    // 좌클릭: 외부 히트 테스트 콜백 먼저
                    if (e.button == 0)
                    {
                        onMouseDown?.Invoke(e, rect);
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    else if (e.button == 1 || e.button == 2)
                    {
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        if (e.button == 0 && dragSlotIdx >= 0)
                        {
                            // 외부 슬롯 드래그 처리
                            onMouseDrag?.Invoke(e, rect, e.delta);
                        }
                        else if (e.button == 0 && !is2D) // 좌클릭: 오빗 (3D만)
                        {
                            orbitAngle += new Vector2(e.delta.x, e.delta.y) * 0.5f;
                            orbitAngle.y = Mathf.Clamp(orbitAngle.y, -89f, 89f);
                        }
                        else if (e.button == 2 || e.button == 1) // 중/우클릭: 패닝
                        {
                            float panScale = zoom * 0.003f;
                            if (is2D)
                            {
                                pan += new Vector3(-e.delta.x * panScale, e.delta.y * panScale, 0f);
                            }
                            else
                            {
                                Camera cam = Preview.camera;
                                Vector3 right = cam.transform.right;
                                Vector3 up = cam.transform.up;
                                pan += (-e.delta.x * right + e.delta.y * up) * panScale;
                            }
                        }
                        e.Use();
                        onRepaint?.Invoke();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        dragSlotIdx = -1;
                        onMouseUp?.Invoke(e);
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }

        // ============================
        // 뷰포트 리사이즈 핸들
        // ============================

        private void DrawViewportResizeHandle(Rect viewportRect)
        {
            float handleH = 6f;
            Rect handleRect = new Rect(viewportRect.x, viewportRect.yMax - handleH,
                viewportRect.width, handleH);

            float barW = 40f;
            Rect barRect = new Rect(
                handleRect.x + (handleRect.width - barW) * 0.5f,
                handleRect.y + 2f, barW, 2f);
            EditorGUI.DrawRect(barRect, new Color(1f, 1f, 1f, 0.3f));

            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeVertical);

            Event e = Event.current;
            int handleId = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && handleRect.Contains(e.mousePosition))
                    {
                        isResizingViewport = true;
                        GUIUtility.hotControl = handleId;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (isResizingViewport && GUIUtility.hotControl == handleId)
                    {
                        viewportHeight += e.delta.y;
                        viewportHeight = Mathf.Clamp(viewportHeight, VIEWPORT_MIN_HEIGHT, VIEWPORT_MAX_HEIGHT);
                        e.Use();
                        onRepaint?.Invoke();
                    }
                    break;

                case EventType.MouseUp:
                    if (isResizingViewport)
                    {
                        isResizingViewport = false;
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }

        // ============================
        // 빈 뷰포트 표시
        // ============================

        private void DrawEmptyViewport(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.05f, 0.07f, 0.1f));
            float b = 1f;
            Color bc = new Color(0.15f, 0.2f, 0.25f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, b), bc);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - b, rect.width, b), bc);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, b, rect.height), bc);
            EditorGUI.DrawRect(new Rect(rect.xMax - b, rect.y, b, rect.height), bc);
            EditorGUI.LabelField(rect, "visualPrefab을 설정하면 프리뷰가 표시됩니다",
                EditorStyles.centeredGreyMiniLabel);
        }

        // ============================
        // XYZ 축 기즈모
        // ============================

        private void DrawAxisGizmo(Rect viewportRect)
        {
            float size = 30f;
            float padding = 34f;
            Vector2 center = new Vector2(viewportRect.xMax - padding - size * 0.5f,
                viewportRect.y + padding + size * 0.5f);

            Quaternion camRot = GetCameraRotation();
            Matrix4x4 viewMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(camRot), Vector3.one);

            Vector3 xDir = viewMatrix.MultiplyVector(Vector3.right);
            Vector3 yDir = viewMatrix.MultiplyVector(Vector3.up);
            Vector3 zDir = viewMatrix.MultiplyVector(Vector3.forward);

            Vector2 xEnd = center + new Vector2(xDir.x, -xDir.y) * size;
            Vector2 yEnd = center + new Vector2(yDir.x, -yDir.y) * size;
            Vector2 zEnd = center + new Vector2(zDir.x, -zDir.y) * size;

            // 배경 원
            Handles.color = new Color(0f, 0f, 0f, 0.3f);
            Handles.DrawSolidDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, size + 4f);

            // 축 선
            DrawAxisLine(center, xEnd, new Color(1f, 0.2f, 0.2f), "X");
            DrawAxisLine(center, yEnd, new Color(0.2f, 1f, 0.2f), "Y");
            DrawAxisLine(center, zEnd, new Color(0.3f, 0.5f, 1f), "Z");
        }

        private void DrawAxisLine(Vector2 from, Vector2 to, Color color, string label)
        {
            Handles.color = color;
            Handles.DrawLine(
                new Vector3(from.x, from.y, 0f),
                new Vector3(to.x, to.y, 0f));

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = color },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            Vector2 labelPos = to + (to - from).normalized * 6f;
            GUI.Label(new Rect(labelPos.x - 8f, labelPos.y - 7f, 16f, 14f), label, style);
        }

        // ============================
        // 디스크 메시 생성
        // ============================

        private Mesh CreateDiskMesh(int segments)
        {
            var mesh = new Mesh { name = "DiskMarker" };
            int vertCount = segments + 1;
            var verts = new Vector3[vertCount];
            var tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
