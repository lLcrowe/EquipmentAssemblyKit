using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 경로(Path) 위에 오브젝트를 일정 간격으로 배치하는 Stateless 모듈.
    /// 포인트 개수에 따라 Line(2), Arc(3), Bezier(4+) 자동 판단.
    /// </summary>
    public static class PathPlacementModule
    {
        public enum SpacingMode
        {
            /// <summary> 고정 거리 간격으로 배치 </summary>
            FixedDistance,
            /// <summary> 경로 전체를 개수 기반 균등 분할 </summary>
            EvenDistribute
        }

        // ─────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────

        /// <summary>
        /// 경로 위에 Transform 배열을 배치한다.
        /// </summary>
        /// <param name="targets">배치할 Transform 배열</param>
        /// <param name="pathPoints">경로 정의 포인트 (2=Line, 3=Arc, 4+=Bezier)</param>
        /// <param name="mode">간격 모드</param>
        /// <param name="fixedDistance">FixedDistance 모드일 때 간격 거리</param>
        /// <param name="offset">경로 시작점 오프셋 (0~)</param>
        /// <param name="alignToPath">경로 방향으로 회전 정렬 여부</param>
        public static void Place(
            Transform[] targets,
            Vector3[] pathPoints,
            SpacingMode mode,
            float fixedDistance = 1f,
            float offset = 0f,
            bool alignToPath = true)
        {
            if (targets == null || targets.Length == 0) return;
            if (pathPoints == null || pathPoints.Length < 2) return;

            float totalLength = CalculatePathLength(pathPoints);
            if (totalLength < 1e-6f) return;

            int count = targets.Length;
            float step = mode switch
            {
                SpacingMode.FixedDistance => fixedDistance,
                SpacingMode.EvenDistribute => totalLength / Mathf.Max(1, count - 1),
                _ => fixedDistance
            };

            // EvenDistribute + 1개일 때 시작점에 배치
            if (mode == SpacingMode.EvenDistribute && count == 1)
                step = 0f;

            for (int i = 0; i < count; i++)
            {
                if (targets[i] == null) continue;

                float dist = (i * step) + offset;
                SamplePath(pathPoints, dist, totalLength, out Vector3 pos, out Vector3 dir);

                targets[i].position = pos;
                if (alignToPath && dir.sqrMagnitude > 1e-6f)
                {
                    targets[i].rotation = Quaternion.LookRotation(Vector3.forward, dir)
                                        * Quaternion.Euler(0f, 0f, -90f);
                }
            }
        }

        /// <summary>
        /// 경로의 총 길이를 반환한다.
        /// </summary>
        public static float CalculatePathLength(Vector3[] pathPoints)
        {
            if (pathPoints == null || pathPoints.Length < 2) return 0f;

            const int segments = 64;
            float length = 0f;
            Vector3 prev = EvaluatePath(pathPoints, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 curr = EvaluatePath(pathPoints, t);
                length += Vector3.Distance(prev, curr);
                prev = curr;
            }

            return length;
        }

        /// <summary>
        /// 경로 상의 t(0~1) 지점 위치를 반환한다.
        /// </summary>
        public static Vector3 EvaluatePath(Vector3[] pathPoints, float t)
        {
            if (pathPoints == null || pathPoints.Length < 2)
                return Vector3.zero;

            t = Mathf.Clamp01(t);
            int count = pathPoints.Length;

            // 2점: Line
            if (count == 2)
                return Vector3.Lerp(pathPoints[0], pathPoints[1], t);

            // 3점: Quadratic Bezier
            if (count == 3)
                return QuadraticBezier(pathPoints[0], pathPoints[1], pathPoints[2], t);

            // 4점+: Catmull-Rom Spline
            return CatmullRomChain(pathPoints, t);
        }

        // ─────────────────────────────────────────
        //  Arc-Length Reparameterization
        // ─────────────────────────────────────────

        /// <summary>
        /// 경로의 누적 호 길이 테이블을 구축한다.
        /// 반환 배열 크기 = resolution + 1.
        /// arcLengths[0] = 0, arcLengths[resolution] = totalLength.
        /// </summary>
        public static float[] BuildArcLengthTable(Vector3[] pathPoints, int resolution = 64)
        {
            if (pathPoints == null || pathPoints.Length < 2)
                return new float[] { 0f };

            var table = new float[resolution + 1];
            table[0] = 0f;

            Vector3 prev = EvaluatePath(pathPoints, 0f);
            for (int i = 1; i <= resolution; i++)
            {
                float t = i / (float)resolution;
                Vector3 curr = EvaluatePath(pathPoints, t);
                table[i] = table[i - 1] + Vector3.Distance(prev, curr);
                prev = curr;
            }

            return table;
        }

        /// <summary>
        /// BuildArcLengthTable의 NonAlloc 버전. 기존 배열을 재사용하여 GC 0 달성.
        /// 배열 크기가 맞지 않으면 ref로 재할당한다.
        /// </summary>
        public static float BuildArcLengthTableNonAlloc(Vector3[] pathPoints, ref float[] table, int resolution = 64)
        {
            int needed = resolution + 1;
            if (pathPoints == null || pathPoints.Length < 2)
            {
                if (table == null || table.Length < 1) table = new float[1];
                table[0] = 0f;
                return 0f;
            }

            if (table == null || table.Length != needed)
                table = new float[needed];

            table[0] = 0f;
            Vector3 prev = EvaluatePath(pathPoints, 0f);
            for (int i = 1; i <= resolution; i++)
            {
                float t = i / (float)resolution;
                Vector3 curr = EvaluatePath(pathPoints, t);
                table[i] = table[i - 1] + Vector3.Distance(prev, curr);
                prev = curr;
            }

            return table[resolution];
        }

        /// <summary>
        /// 거리 → 파라미터 t 역산. 이진 탐색으로 arcLengths 테이블에서 정확한 t를 구한다.
        /// </summary>
        public static float DistanceToParameter(float[] arcLengths, float distance, float totalLength)
        {
            if (arcLengths == null || arcLengths.Length < 2) return 0f;
            if (totalLength < 1e-6f) return 0f;

            distance = Mathf.Clamp(distance, 0f, totalLength);

            int resolution = arcLengths.Length - 1;

            // 이진 탐색: distance가 속한 구간 [lo, lo+1] 찾기
            int lo = 0;
            int hi = resolution;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) / 2;
                if (arcLengths[mid] <= distance)
                    lo = mid;
                else
                    hi = mid;
            }

            // 구간 내 선형 보간
            float segLength = arcLengths[lo + 1] - arcLengths[lo];
            float segFraction = segLength > 1e-6f
                ? (distance - arcLengths[lo]) / segLength
                : 0f;

            return (lo + segFraction) / resolution;
        }

        // ─────────────────────────────────────────
        //  Path Sampling
        // ─────────────────────────────────────────

        /// <summary>
        /// distance(거리) 기반으로 경로 위의 위치와 방향을 샘플링한다.
        /// 선형 가정 버전 (하위호환).
        /// </summary>
        public static void SamplePath(
            Vector3[] pathPoints,
            float distance,
            float totalLength,
            out Vector3 position,
            out Vector3 direction)
        {
            if (totalLength < 1e-6f)
            {
                position = pathPoints != null && pathPoints.Length > 0 ? pathPoints[0] : Vector3.zero;
                direction = Vector3.right;
                return;
            }

            float t = Mathf.Clamp01(distance / totalLength);
            position = EvaluatePath(pathPoints, t);
            direction = EvaluateTangent(pathPoints, t);
        }

        /// <summary>
        /// Arc-Length 테이블 기반 정확한 샘플링. 곡선 구간에서도 균일 간격 보장.
        /// </summary>
        public static void SamplePath(
            Vector3[] pathPoints,
            float distance,
            float[] arcLengths,
            float totalLength,
            out Vector3 position,
            out Vector3 direction)
        {
            if (totalLength < 1e-6f)
            {
                position = pathPoints != null && pathPoints.Length > 0 ? pathPoints[0] : Vector3.zero;
                direction = Vector3.right;
                return;
            }

            float t = DistanceToParameter(arcLengths, distance, totalLength);
            position = EvaluatePath(pathPoints, t);
            direction = EvaluateTangent(pathPoints, t);
        }

        /// <summary>
        /// 경로 상의 t 지점에서 탄젠트(방향) 벡터를 구한다.
        /// </summary>
        public static Vector3 EvaluateTangent(Vector3[] pathPoints, float t)
        {
            const float delta = 0.001f;
            float tNext = Mathf.Min(t + delta, 1f);
            float tPrev = Mathf.Max(t - delta, 0f);

            Vector3 pNext = EvaluatePath(pathPoints, tNext);
            Vector3 pPrev = EvaluatePath(pathPoints, tPrev);
            Vector3 dir = (pNext - pPrev).normalized;

            if (dir.sqrMagnitude < 1e-6f)
                return Vector3.right;
            return dir;
        }

        // ─────────────────────────────────────────
        //  Pipe Mesh Generation
        // ─────────────────────────────────────────

        /// <summary>
        /// 경로를 따라 프로시저럴 파이프 메쉬를 생성한다.
        /// Arc-Length 테이블 기반 균등 샘플링으로 곡선에서도 균일한 단면 간격 보장.
        /// </summary>
        public static Mesh GeneratePipeMesh(
            Vector3[] pathPoints,
            float[] arcLengths,
            float totalLength,
            float radius,
            int circleSegments = 12,
            int pathResolution = 20,
            float uvTiling = 1f)
        {
            var mesh = new Mesh();
            mesh.name = "ConnectorPipe";
            var buffers = new PipeMeshBuffers();
            GeneratePipeMeshNonAlloc(pathPoints, arcLengths, totalLength,
                radius, circleSegments, pathResolution, uvTiling, mesh, buffers);
            return mesh;
        }

        /// <summary>
        /// GC-free 파이프 메쉬 생성. 기존 Mesh와 버퍼를 재사용한다.
        /// buffers가 null이면 내부에서 할당하지만, 캐싱해서 매 프레임 전달하면 GC 0.
        /// </summary>
        public static void GeneratePipeMeshNonAlloc(
            Vector3[] pathPoints,
            float[] arcLengths,
            float totalLength,
            float radius,
            int circleSegments,
            int pathResolution,
            float uvTiling,
            Mesh targetMesh,
            PipeMeshBuffers buffers)
        {
            if (pathPoints == null || pathPoints.Length < 2 || totalLength < 1e-6f || targetMesh == null)
                return;

            int ringCount = pathResolution + 1;
            buffers.EnsureCapacity(ringCount, circleSegments);

            // 경로 샘플링 (Arc-Length 균등)
            for (int i = 0; i < ringCount; i++)
            {
                float dist = (i / (float)pathResolution) * totalLength;
                SamplePath(pathPoints, dist, arcLengths, totalLength,
                    out buffers.ringCenters[i], out buffers.ringTangents[i]);
            }

            // 버텍스/인덱스 생성
            int vertCount = ringCount * circleSegments;

            // 링 방향 프레임 추적 (twist 방지)
            Vector3 prevNormal = CalculateInitialNormal(buffers.ringTangents[0]);

            for (int ring = 0; ring < ringCount; ring++)
            {
                Vector3 tangent = buffers.ringTangents[ring];

                // Rotation Minimizing Frame
                Vector3 projected = (prevNormal - Vector3.Dot(prevNormal, tangent) * tangent).normalized;
                if (projected.sqrMagnitude < 1e-6f)
                    projected = CalculateInitialNormal(tangent);
                prevNormal = projected;

                Vector3 binormal = Vector3.Cross(tangent, projected).normalized;

                float pathT = ring / (float)pathResolution;
                int baseIdx = ring * circleSegments;

                for (int seg = 0; seg < circleSegments; seg++)
                {
                    float angle = (seg / (float)circleSegments) * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * radius;
                    float y = Mathf.Sin(angle) * radius;

                    Vector3 localOffset = projected * x + binormal * y;
                    int idx = baseIdx + seg;

                    buffers.vertices[idx] = buffers.ringCenters[ring] + localOffset;
                    buffers.normals[idx] = localOffset.normalized;
                    buffers.uvs[idx] = new Vector2(
                        (seg / (float)circleSegments) * uvTiling,
                        pathT * uvTiling);
                }
            }

            // Cap 중심점
            int startCapIdx = vertCount;
            int endCapIdx = vertCount + 1;
            buffers.vertices[startCapIdx] = buffers.ringCenters[0];
            buffers.normals[startCapIdx] = -buffers.ringTangents[0];
            buffers.uvs[startCapIdx] = new Vector2(0.5f, 0.5f);

            buffers.vertices[endCapIdx] = buffers.ringCenters[ringCount - 1];
            buffers.normals[endCapIdx] = buffers.ringTangents[ringCount - 1];
            buffers.uvs[endCapIdx] = new Vector2(0.5f, 0.5f);

            // 삼각형은 토폴로지 불변이므로 첫 빌드 시만 계산
            if (buffers.trianglesDirty)
            {
                int tri = 0;

                // 링 연결
                for (int ring = 0; ring < ringCount - 1; ring++)
                {
                    int cur = ring * circleSegments;
                    int next = (ring + 1) * circleSegments;

                    for (int seg = 0; seg < circleSegments; seg++)
                    {
                        int c0 = cur + seg;
                        int c1 = cur + (seg + 1) % circleSegments;
                        int n0 = next + seg;
                        int n1 = next + (seg + 1) % circleSegments;

                        buffers.triangles[tri++] = c0;
                        buffers.triangles[tri++] = n0;
                        buffers.triangles[tri++] = c1;

                        buffers.triangles[tri++] = c1;
                        buffers.triangles[tri++] = n0;
                        buffers.triangles[tri++] = n1;
                    }
                }

                // 시작 Cap
                for (int seg = 0; seg < circleSegments; seg++)
                {
                    int c0 = seg;
                    int c1 = (seg + 1) % circleSegments;
                    buffers.triangles[tri++] = startCapIdx;
                    buffers.triangles[tri++] = c1;
                    buffers.triangles[tri++] = c0;
                }

                // 끝 Cap
                int lastRingBase = (ringCount - 1) * circleSegments;
                for (int seg = 0; seg < circleSegments; seg++)
                {
                    int c0 = lastRingBase + seg;
                    int c1 = lastRingBase + (seg + 1) % circleSegments;
                    buffers.triangles[tri++] = endCapIdx;
                    buffers.triangles[tri++] = c0;
                    buffers.triangles[tri++] = c1;
                }

                buffers.trianglesDirty = false;
            }

            targetMesh.Clear();
            targetMesh.vertices = buffers.vertices;
            targetMesh.normals = buffers.normals;
            targetMesh.uv = buffers.uvs;
            targetMesh.triangles = buffers.triangles;
        }

        /// <summary>
        /// GeneratePipeMeshNonAlloc용 재사용 버퍼.
        /// PartConnectorVisual 등에서 캐싱하여 GC 0 달성.
        /// </summary>
        public class PipeMeshBuffers
        {
            public Vector3[] ringCenters;
            public Vector3[] ringTangents;
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uvs;
            public int[] triangles;
            public bool trianglesDirty = true;

            int cachedRingCount;
            int cachedCircleSegments;

            public void EnsureCapacity(int ringCount, int circleSegments)
            {
                if (cachedRingCount == ringCount && cachedCircleSegments == circleSegments)
                    return;

                cachedRingCount = ringCount;
                cachedCircleSegments = circleSegments;

                ringCenters = new Vector3[ringCount];
                ringTangents = new Vector3[ringCount];

                int vertCount = ringCount * circleSegments + 2; // +2 cap centers
                vertices = new Vector3[vertCount];
                normals = new Vector3[vertCount];
                uvs = new Vector2[vertCount];

                int bodyTriCount = (ringCount - 1) * circleSegments * 6;
                int capTriCount = circleSegments * 3 * 2;
                triangles = new int[bodyTriCount + capTriCount];
                trianglesDirty = true;
            }
        }

        private static Vector3 CalculateInitialNormal(in Vector3 tangent)
        {
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.99f)
                up = Vector3.right;
            return Vector3.Cross(tangent, up).normalized;
        }

        // ─────────────────────────────────────────
        //  Curve Math
        // ─────────────────────────────────────────

        public static Vector3 QuadraticBezier(
            in Vector3 p0, in Vector3 p1, in Vector3 p2, float t)
        {
            float u = 1f - t;
            return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
        }

        private static Vector3 CatmullRomChain(Vector3[] points, float t)
        {
            int count = points.Length;
            int segmentCount = count - 1;
            float scaledT = t * segmentCount;
            int seg = Mathf.Clamp((int)scaledT, 0, segmentCount - 1);
            float localT = scaledT - seg;

            Vector3 p0 = points[Mathf.Max(seg - 1, 0)];
            Vector3 p1 = points[seg];
            Vector3 p2 = points[Mathf.Min(seg + 1, count - 1)];
            Vector3 p3 = points[Mathf.Min(seg + 2, count - 1)];

            return CatmullRom(p0, p1, p2, p3, localT);
        }

        private static Vector3 CatmullRom(
            in Vector3 p0, in Vector3 p1, in Vector3 p2, in Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
}
