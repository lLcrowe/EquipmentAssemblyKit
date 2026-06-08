using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// GL 기반 에디터 기즈모 렌더러.
    /// 디스크 마커, 와이어 원, 선, 방향 화살표 등을 GL.PushMatrix 내부에서 그린다.
    /// EditorViewport와 함께 사용하며, 장비/차량/로봇 에디터에서 재사용한다.
    /// </summary>
    public class EditorGizmoRenderer
    {
        private Material markerMat;
        private Material lineMat;
        private Mesh diskMesh;

        public void Initialize(Material markerMat, Material lineMat, Mesh diskMesh)
        {
            this.markerMat = markerMat;
            this.lineMat = lineMat;
            this.diskMesh = diskMesh;
        }

        /// <summary>EditorViewport에서 리소스를 받아 초기화한다.</summary>
        public void Initialize(EditorViewport viewport)
        {
            markerMat = viewport.MarkerMaterial;
            lineMat = viewport.LineMaterial;
            diskMesh = viewport.DiskMesh;
        }

        // ============================
        // 기본 GL 프리미티브
        // ============================

        /// <summary>GL 와이어 원 (24 세그먼트)</summary>
        public void DrawWireCircle(Vector3 center, Quaternion rot, float radius, Color color)
        {
            if (lineMat == null) return;
            lineMat.SetPass(0);
            GL.Begin(GL.LINE_STRIP);
            GL.Color(color);
            int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                GL.Vertex(center + rot * local);
            }
            GL.End();
        }

        /// <summary>GL 선분</summary>
        public void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            if (lineMat == null) return;
            lineMat.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex(from);
            GL.Vertex(to);
            GL.End();
        }

        /// <summary>채움 디스크 (Material.color + SetPass + DrawMeshNow)</summary>
        public void DrawFilledDisk(Vector3 pos, Quaternion rot, float scale, Color color)
        {
            if (markerMat == null || diskMesh == null) return;
            Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, Vector3.one * scale);
            markerMat.color = color;
            markerMat.SetPass(0);
            Graphics.DrawMeshNow(diskMesh, matrix);
        }

        // ============================
        // 복합 기즈모
        // ============================

        /// <summary>
        /// 방향 화살표를 그린다 (선 + 화살촉).
        /// direction은 정규화된 방향 벡터.
        /// </summary>
        public void DrawDirectionArrow(Vector3 origin, Vector3 direction, Quaternion rot, float length, Color color)
        {
            Vector3 tip = origin + direction * length;
            DrawLine(origin, tip, color);

            // 화살촉
            Vector3 side = rot * Vector3.right;
            float headSize = length * 0.3f;
            DrawLine(tip, tip - direction * headSize + side * headSize, color);
            DrawLine(tip, tip - direction * headSize - side * headSize, color);
        }

        /// <summary>
        /// 슬롯 마커를 그린다 (채움 디스크 + 와이어 테두리).
        /// showDirection이 true이면 방향 화살표도 추가한다.
        /// </summary>
        public void DrawSlotMarker(Vector3 pos, Quaternion rot, float scale,
            Color fillColor, Color wireColor, bool is2D, bool showDirection = true)
        {
            // 채움 디스크
            DrawFilledDisk(pos, rot, scale, fillColor);

            // 와이어 테두리
            DrawWireCircle(pos, rot, scale, wireColor);

            // 방향 화살표
            if (showDirection)
            {
                Vector3 dir = is2D ? (rot * Vector3.up) : (rot * Vector3.forward);
                DrawDirectionArrow(pos, dir, rot, scale * 2.5f, wireColor);
            }
        }

        /// <summary>
        /// 피봇 기즈모를 그린다 (와이어만, 정방향 화살표 포함).
        /// 슬롯 위에 오버레이되므로 디스크 없이 와이어만 사용.
        /// </summary>
        public void DrawPivotMarker(Vector3 origin, Quaternion rot, float scale,
            Color pivotColor, Color forwardColor, bool is2D)
        {
            // 와이어 원만
            DrawWireCircle(origin, rot, scale, pivotColor);

            // 정방향 화살표
            Vector3 dir = is2D ? (rot * Vector3.up) : (rot * Vector3.forward);
            DrawDirectionArrow(origin, dir, rot, scale * 2.5f, forwardColor);
        }

        /// <summary>
        /// 오프셋 기즈모를 그린다 (위치/회전 오프셋 시각화).
        /// </summary>
        public void DrawOffsetMarker(Vector3 origin, Vector3 pivotPos, Quaternion pivotRot,
            float scale, Color offsetColor, bool is2D, bool showConnectionLine = true)
        {
            // 와이어 원
            DrawWireCircle(pivotPos, pivotRot, scale * 0.8f, offsetColor);

            // 방향 화살표
            Vector3 oDir = is2D ? (pivotRot * Vector3.up) : (pivotRot * Vector3.forward);
            DrawDirectionArrow(pivotPos, oDir, pivotRot, scale * 0.8f * 2.5f, offsetColor);

            // 연결선
            if (showConnectionLine)
                DrawLine(origin, pivotPos, new Color(offsetColor.r, offsetColor.g, offsetColor.b, 0.4f));
        }

        /// <summary>부모-자식 연결선 (반투명)</summary>
        public void DrawConnectionLine(Vector3 parentPos, Vector3 childPos, Color markerColor)
        {
            if (parentPos == childPos) return;
            DrawLine(parentPos, childPos, new Color(markerColor.r, markerColor.g, markerColor.b, 0.4f));
        }

        /// <summary>필수 슬롯 강조용 이중 와이어 원</summary>
        public void DrawRequiredRing(Vector3 pos, Quaternion rot, float baseScale, Color requiredColor)
        {
            DrawWireCircle(pos, rot, baseScale * 1.4f, requiredColor);
        }
    }
}
