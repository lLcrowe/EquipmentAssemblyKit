using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit
{
    /// <summary>
    /// 파츠 연결 비주얼. PathPlacementModule 기반.
    /// 두 파츠 사이를 곡선으로 잇는 비주얼을 생성한다.
    /// Pipe: 프로시저럴 3D 파이프 메쉬 (케이블, 호스), PlaceObjects: 오브젝트 반복 배치 (탄환링크, 체인).
    /// PartSlot.connectVisualPrefab에 프리팹으로 할당.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("lLCroweTool/Equipment/Part Connector Visual")]
    [HelpURL("")]
    public class PartConnectorVisual : MonoBehaviour
    {
        public enum ConnectorMode
        {
            [Tooltip("프로시저럴 3D 파이프 메쉬.\n용도: 파워 케이블, 유압 호스, 냉각관 등 매끈한 튜브 형태")]
            Pipe,
            [Tooltip("프리팹을 경로 위에 반복 배치.\n용도: 탄띠(ammo belt), 체인고리, 로프 매듭 등 반복 오브젝트.\nscrollSpeed로 탄띠 이동 애니메이션 가능")]
            PlaceObjects
        }

        // ═══════════════════════════════════════════
        //  기본 설정
        // ═══════════════════════════════════════════

        [Header("── 모드 ──────────────────────────")]
        [Tooltip("Pipe: 프로시저럴 3D 파이프 메쉬 (케이블, 호스, 전선)\n" +
                 "PlaceObjects: 프리팹 반복 배치 (탄띠, 체인)\n\n" +
                 "▶ 탄띠 구성 예시: Pipe(외피) + PlaceObjects(탄약) 2개 조합\n" +
                 "  ① 외피: Pipe 모드, renderMesh=true, 벨트 머티리얼\n" +
                 "  ② 탄약: PlaceObjects 모드, 탄약 프리팹, scrollSpeed로 이동")]
        public ConnectorMode mode = ConnectorMode.Pipe;

        // ═══════════════════════════════════════════
        //  연결 대상
        // ═══════════════════════════════════════════

        [Header("── 연결 대상 ─────────────────────")]
        [Tooltip("시작 지점 Transform. 런타임에서는 Connect()로 설정 가능")]
        public Transform startPoint;

        [Tooltip("끝 지점 Transform. 런타임에서는 Connect()로 설정 가능")]
        public Transform endPoint;

        // ═══════════════════════════════════════════
        //  경로 곡선
        // ═══════════════════════════════════════════

        [Header("── 경로 곡선 ─────────────────────")]
        [Tooltip("시작~끝 사이 중간 제어점.\n비어있으면 droopAmount로 자동 처짐 곡선 생성.\n1개 = 3점 베지어, 2개+ = Catmull-Rom 스플라인")]
        public Transform[] midAnchors = new Transform[0];

        [Tooltip("최대 처짐량. 슬랙이 100%일 때 이만큼 처진다.\nmidAnchors가 비어있을 때만 적용")]
        [Range(0f, 5f)]
        public float droopAmount = 0.3f;

        [Tooltip("처짐 방향. 보통 (0,-1,0)=아래. 2D에서도 동일")]
        public Vector3 droopDirection = Vector3.down;

        [Tooltip("처짐 강성. 낮을수록 빳빳 (여유가 생겨도 덜 처짐)\n높을수록 유연 (조금만 여유 생겨도 확 처짐)\nmaxLength가 0이면 무시")]
        [Range(0.1f, 3f)]
        public float stiffness = 1f;

        // ═══════════════════════════════════════════
        //  스프링 시뮬레이션 (런타임 전용)
        // ═══════════════════════════════════════════

        [Header("── 스프링 (런타임) ──────────────────")]
        [Tooltip("활성화 시 중간 제어점에 스프링 관성 적용.\n캐릭터 이동 시 로프처럼 출렁임")]
        public bool enableSpring = false;

        [Tooltip("스프링 복원력. 클수록 빨리 원위치로 돌아옴")]
        [Range(1f, 100f)]
        public float springForce = 20f;

        [Tooltip("감쇠. 클수록 빨리 멈춤")]
        [Range(0.1f, 20f)]
        public float springDamping = 5f;

        [Tooltip("스프링 중력. droopDirection 방향 추가 가속도")]
        [Range(0f, 20f)]
        public float springGravity = 2f;

        [Tooltip("midAnchors와 1:1 매칭. true인 앵커는 스프링 영향 안 받고 고정됨.\n벽 고리에 걸린 로프, 고정점 사이만 출렁임")]
        public bool[] pinnedAnchors = new bool[0];

        [Tooltip("스프링이 원위치에서 벗어날 수 있는 최대 거리.\n0이면 무제한")]
        [Range(0f, 5f)]
        public float springMaxDisplacement = 0f;

        // ═══════════════════════════════════════════
        //  공통 옵션
        // ═══════════════════════════════════════════

        [Header("── 공통 ──────────────────────────")]
        [Tooltip("0 이하면 무제한.\n초과 시 끝점을 최대 길이 지점으로 클램프")]
        public float maxLength = 0f;

        [Tooltip("체크 시 2D 모드.\n회전: Z축 기반 (Atan2)")]
        public bool is2D = false;

        [Tooltip("배치된 오브젝트/메쉬를 경로 방향으로 회전 정렬")]
        public bool alignToPath = true;

        // ═══════════════════════════════════════════
        //  Pipe 모드 전용
        // ═══════════════════════════════════════════

        [Header("── Pipe 모드 ─────────────────────")]
        [Tooltip("파이프 반지름")]
        [Range(0.001f, 1f)]
        public float pipeRadius = 0.01f;

        [Tooltip("파이프 단면 분할 수. 높을수록 둥글지만 비용 증가")]
        [Range(3, 24)]
        public int circleSegments = 8;

        [Tooltip("경로 분할 수. 높을수록 부드러운 곡선")]
        [Range(4, 100)]
        public int pathResolution = 20;

        [Tooltip("UV 반복 횟수")]
        public float uvTiling = 1f;

        [Tooltip("파이프에 사용할 머티리얼. 비어있으면 기본 머티리얼 생성")]
        public Material pipeMaterial;

        [Tooltip("체크 시 실제 메쉬 렌더링. 해제 시 기즈모로만 표시")]
        public bool renderMesh = false;

        // ═══════════════════════════════════════════
        //  PlaceObjects 모드 전용
        // ═══════════════════════════════════════════

        [Header("── PlaceObjects 모드 ─────────────")]
        [Tooltip("경로 위에 반복 배치할 프리팹.\n예: 탄환링크, 체인고리, 로프 매듭")]
        public GameObject objectPrefab;

        [Tooltip("FixedDistance: 고정 거리마다 배치\nEvenDistribute: 경로를 maxObjectCount로 균등 분할")]
        public PathPlacementModule.SpacingMode spacingMode = PathPlacementModule.SpacingMode.EvenDistribute;

        [Tooltip("FixedDistance 모드 전용. 오브젝트 사이 간격 (월드 단위)")]
        [Range(0.001f, 10f)]
        public float fixedDistance = 0.1f;

        [Tooltip("최대 배치 개수. 성능 보호용 상한")]
        [Range(1, 200)]
        public int maxObjectCount = 50;

        [Tooltip("배치 오브젝트의 로컬 위치 오프셋.\n경로 기준 (X=우, Y=상, Z=전방)")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("배치 오브젝트의 추가 회전 오프셋 (오일러각).\n프리팹의 기본 방향이 경로와 맞지 않을 때 보정")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("오브젝트 스크롤 속도 (월드 단위/초).\n양수: start→end 방향, 음수: 역방향.\n0이면 정지.\n용도: 캐틀링건 탄띠 발사 시 이동 애니메이션")]
        public float scrollSpeed = 0f;

        // ═══════════════════════════════════════════
        //  런타임 상태 (Inspector 노출 안 됨)
        // ═══════════════════════════════════════════

        Vector3[] pathPointsCache;
        Transform[] placedObjectsCache;
        int activePlacedCount;

        // Arc-Length 테이블 캐시
        float[] arcLengthTable;
        float cachedTotalLength;

        // Pipe 메쉬 캐시
        MeshFilter pipeMeshFilter;
        MeshRenderer pipeMeshRenderer;
        Mesh pipeMesh;
        PathPlacementModule.PipeMeshBuffers pipeMeshBuffers;

        // 스프링 시뮬레이션 상태
        Vector3[] springPositions;
        Vector3[] springVelocities;
        int springMidCount;

        // PlaceObjects 스크롤 상태
        float scrollOffset;


        // ─────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────

        void OnEnable()
        {
            // 컴포넌트 셋업은 OnEnable에서 1회
            if (mode == ConnectorMode.Pipe && renderMesh)
                EnsurePipeComponents();
        }

        void Update()
        {
            if (startPoint == null || endPoint == null)
            {
                ClearVisual();
                return;
            }

            RebuildPathPoints();
            UpdateVisual();
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && startPoint != null && endPoint != null)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    RebuildPathPoints();
                    UpdateVisual();
                    UnityEditor.SceneView.RepaintAll();
                };
            }
#endif
        }

        void OnDisable()
        {
            ClearVisual();
        }

        // ─────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────

        /// <summary>
        /// Transform 기반 연결. startPoint/endPoint를 설정하고 즉시 갱신한다.
        /// </summary>
        public void Connect(Transform start, Transform end)
        {
            startPoint = start;
            endPoint = end;
        }

        /// <summary>
        /// 연결 해제.
        /// </summary>
        public void Disconnect()
        {
            startPoint = null;
            endPoint = null;
            springPositions = null;
            springVelocities = null;
            springMidCount = 0;
            ClearVisual();
        }

        /// <summary>
        /// 현재 연결 상태.
        /// </summary>
        public bool IsConnected => startPoint != null && endPoint != null;

        /// <summary>
        /// 현재 경로 포인트 반환 (에디터 기즈모용).
        /// </summary>
        public Vector3[] GetPathPoints()
        {
            return pathPointsCache;
        }

        /// <summary>
        /// 스크롤 오프셋 직접 설정 (외부 시스템에서 발사 속도 연동 시).
        /// scrollSpeed와 독립적으로 사용 가능.
        /// </summary>
        public float ScrollOffset
        {
            get => scrollOffset;
            set => scrollOffset = value;
        }

        /// <summary>
        /// RPM 기반 스크롤 업데이트. 매 프레임 호출하면 발사 속도에 동기화된다.
        /// 예열(spin-up): currentRPM을 0→maxRPM으로 서서히 올리면 탄띠도 서서히 빨라짐.
        /// 정지: currentRPM=0이면 탄띠 정지.
        /// </summary>
        /// <param name="currentRPM">현재 분당 발사 수 (0이면 정지)</param>
        /// <param name="dt">Time.deltaTime</param>
        public void ScrollByRPM(float currentRPM, float dt)
        {
            scrollOffset += (currentRPM / 60f) * fixedDistance * dt;
        }

        // ─────────────────────────────────────────
        //  Internal
        // ─────────────────────────────────────────

        void RebuildPathPoints()
        {
            Vector3 startPos = startPoint.position;
            Vector3 endPos = endPoint.position;

            int midCount = 0;
            if (midAnchors != null)
            {
                for (int i = 0; i < midAnchors.Length; i++)
                {
                    if (midAnchors[i] != null) midCount++;
                }
            }

            if (midCount == 0)
            {
                if (pathPointsCache == null || pathPointsCache.Length != 3)
                    pathPointsCache = new Vector3[3];

                // 슬랙 기반 처짐: maxLength > 0이면 여유분에 비례, 아니면 droopAmount 그대로
                float actualDroop = droopAmount;
                if (maxLength > 0f)
                {
                    float dist = Vector3.Distance(startPos, endPos);
                    float slackRatio = Mathf.Clamp01((maxLength - dist) / maxLength);
                    actualDroop = droopAmount * Mathf.Pow(slackRatio, 1f / stiffness);
                }

                pathPointsCache[0] = startPos;
                pathPointsCache[1] = (startPos + endPos) * 0.5f + droopDirection * actualDroop;
                pathPointsCache[2] = endPos;
            }
            else
            {
                int total = 2 + midCount;
                if (pathPointsCache == null || pathPointsCache.Length != total)
                    pathPointsCache = new Vector3[total];

                pathPointsCache[0] = startPos;
                int idx = 1;
                for (int i = 0; i < midAnchors.Length; i++)
                {
                    if (midAnchors[i] != null)
                        pathPointsCache[idx++] = midAnchors[i].position;
                }
                pathPointsCache[total - 1] = endPos;
            }

            // 스프링 시뮬레이션
            if (enableSpring)
            {
                float dt = Application.isPlaying
                    ? Time.deltaTime
                    : 0.016f;
                SimulateSpring(dt);
            }

            // 최대 길이 클램프 — 경로 실측 길이 기준
            if (maxLength > 0f)
            {
                ClampPathToMaxLength();
            }

            // Arc-Length 테이블 갱신 (NonAlloc — 배열 재사용으로 GC 0)
            cachedTotalLength = PathPlacementModule.BuildArcLengthTableNonAlloc(pathPointsCache, ref arcLengthTable);
        }

        void ClampPathToMaxLength()
        {
            float accumulated = 0f;
            for (int i = 1; i < pathPointsCache.Length; i++)
            {
                float segLen = Vector3.Distance(pathPointsCache[i - 1], pathPointsCache[i]);
                if (accumulated + segLen >= maxLength)
                {
                    // 이 구간 안에서 잘라야 함
                    float remain = maxLength - accumulated;
                    Vector3 dir = (pathPointsCache[i] - pathPointsCache[i - 1]).normalized;
                    Vector3 clampedEnd = pathPointsCache[i - 1] + dir * remain;

                    // i 위치를 클램프 지점으로 교체하고 뒤는 버림
                    int newLen = i + 1;
                    if (pathPointsCache.Length != newLen)
                    {
                        var trimmed = new Vector3[newLen];
                        for (int j = 0; j < i; j++)
                            trimmed[j] = pathPointsCache[j];
                        trimmed[i] = clampedEnd;
                        pathPointsCache = trimmed;
                    }
                    else
                    {
                        pathPointsCache[i] = clampedEnd;
                    }
                    return;
                }
                accumulated += segLen;
            }
        }

        void SimulateSpring(float dt)
        {
            int midCount = pathPointsCache.Length - 2;
            if (midCount <= 0) return;

            // 초기화 또는 개수 변경 시 리셋
            if (springPositions == null || springMidCount != midCount)
            {
                springPositions = new Vector3[midCount];
                springVelocities = new Vector3[midCount];
                springMidCount = midCount;
                for (int i = 0; i < midCount; i++)
                {
                    springPositions[i] = pathPointsCache[i + 1];
                    springVelocities[i] = Vector3.zero;
                }
                return;
            }

            for (int i = 0; i < midCount; i++)
            {
                // Pin된 앵커는 스프링 시뮬레이션 스킵
                if (pinnedAnchors != null && i < pinnedAnchors.Length && pinnedAnchors[i])
                {
                    springPositions[i] = pathPointsCache[i + 1];
                    springVelocities[i] = Vector3.zero;
                    continue;
                }

                Vector3 restPos = pathPointsCache[i + 1];

                // 스프링 가속도 = 복원력 + 감쇠 + 중력
                Vector3 accel = (restPos - springPositions[i]) * springForce
                              - springVelocities[i] * springDamping
                              + droopDirection * springGravity;

                springVelocities[i] += accel * dt;
                springPositions[i] += springVelocities[i] * dt;

                // 최대 변위 클램프
                if (springMaxDisplacement > 0f)
                {
                    Vector3 displacement = springPositions[i] - restPos;
                    if (displacement.sqrMagnitude > springMaxDisplacement * springMaxDisplacement)
                    {
                        springPositions[i] = restPos + displacement.normalized * springMaxDisplacement;
                        springVelocities[i] = Vector3.zero;
                    }
                }

                // pathPointsCache에 시뮬레이션 결과 반영
                pathPointsCache[i + 1] = springPositions[i];
            }

        }

        void UpdateVisual()
        {
            if (pathPointsCache == null || pathPointsCache.Length < 2) return;

            switch (mode)
            {
                case ConnectorMode.Pipe:
                    UpdatePipe();
                    break;
                case ConnectorMode.PlaceObjects:
                    UpdatePlaceObjects();
                    break;
            }
        }

        void ClearVisual()
        {
            ClearPipe();
            HideAllPlacedObjects();
        }

        // ── Pipe ──

        void UpdatePipe()
        {
            // renderMesh 꺼져 있으면 기즈모로만 표시 (메쉬 생성 안 함)
            if (!renderMesh)
            {
                ClearPipe();
                return;
            }

            // OnEnable에서 셋업되지만, 모드 변경/Inspector 조작 시 폴백
            if (pipeMeshFilter == null || pipeMeshRenderer == null)
                EnsurePipeComponents();

            // Mesh 재사용 (매 프레임 new Mesh 방지)
            if (pipeMesh == null)
            {
                pipeMesh = new Mesh();
                pipeMesh.name = "ConnectorPipe";
            }

            // 버퍼 재사용 (매 프레임 배열 할당 방지)
            if (pipeMeshBuffers == null)
                pipeMeshBuffers = new PathPlacementModule.PipeMeshBuffers();

            PathPlacementModule.GeneratePipeMeshNonAlloc(
                pathPointsCache, arcLengthTable, cachedTotalLength,
                pipeRadius, circleSegments, pathResolution, uvTiling,
                pipeMesh, pipeMeshBuffers);

            pipeMeshFilter.sharedMesh = pipeMesh;
            pipeMeshRenderer.enabled = true;
        }

        void EnsurePipeComponents()
        {
            if (pipeMeshFilter == null)
            {
                pipeMeshFilter = GetComponent<MeshFilter>();
                if (pipeMeshFilter == null)
                    pipeMeshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (pipeMeshRenderer == null)
            {
                pipeMeshRenderer = GetComponent<MeshRenderer>();
                if (pipeMeshRenderer == null)
                {
                    pipeMeshRenderer = gameObject.AddComponent<MeshRenderer>();
                    if (pipeMaterial != null)
                        pipeMeshRenderer.sharedMaterial = pipeMaterial;
                    else
                        pipeMeshRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
            }

            // 머티리얼 변경 감지
            if (pipeMaterial != null && pipeMeshRenderer.sharedMaterial != pipeMaterial)
                pipeMeshRenderer.sharedMaterial = pipeMaterial;
        }

        void ClearPipe()
        {
            if (pipeMeshFilter != null)
                pipeMeshFilter.sharedMesh = null;
            if (pipeMeshRenderer != null)
                pipeMeshRenderer.enabled = false;
        }

        // ── PlaceObjects ──

        void UpdatePlaceObjects()
        {
            float totalLength = cachedTotalLength;
            if (totalLength < 1e-6f) return;

            int count;
            if (spacingMode == PathPlacementModule.SpacingMode.FixedDistance)
            {
                count = Mathf.Max(1, Mathf.FloorToInt(totalLength / Mathf.Max(fixedDistance, 0.001f)) + 1);
            }
            else
            {
                count = maxObjectCount;
            }
            count = Mathf.Min(count, maxObjectCount);

            EnsureObjectPool(count);

            if (activePlacedCount != count)
            {
                for (int i = 0; i < placedObjectsCache.Length; i++)
                {
                    if (placedObjectsCache[i] != null)
                        placedObjectsCache[i].gameObject.SetActive(i < count);
                }
                activePlacedCount = count;
            }

            float step = spacingMode == PathPlacementModule.SpacingMode.FixedDistance
                ? fixedDistance
                : totalLength / Mathf.Max(1, count - 1);

            if (spacingMode == PathPlacementModule.SpacingMode.EvenDistribute && count == 1)
                step = 0f;

            // 스크롤 오프셋 누적
            if (scrollSpeed != 0f)
            {
                float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
                scrollOffset += scrollSpeed * dt;

                // 순환: step 기준으로 wrap (오브젝트 1개 간격만큼 밀리면 리셋)
                if (step > 1e-6f)
                {
                    scrollOffset %= step;
                    if (scrollOffset < 0f) scrollOffset += step;
                }
            }

            // start/end 회전에서 up 벡터 보간 준비
            Vector3 upStart = startPoint.up;
            Vector3 upEnd = endPoint.up;

            for (int i = 0; i < count; i++)
            {
                if (placedObjectsCache[i] == null) continue;

                float dist = i * step + scrollOffset;

                // 경로 범위 클램프 (0 ~ totalLength)
                dist = Mathf.Clamp(dist, 0f, totalLength);

                PathPlacementModule.SamplePath(pathPointsCache, dist, arcLengthTable, totalLength,
                    out Vector3 pos, out Vector3 dir);

                Quaternion baseRot;
                if (alignToPath && dir.sqrMagnitude > 1e-6f)
                {
                    if (is2D)
                    {
                        baseRot = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    }
                    else
                    {
                        float t = totalLength > 1e-6f ? dist / totalLength : 0f;
                        Vector3 up = Vector3.Slerp(upStart, upEnd, t);
                        baseRot = Quaternion.LookRotation(dir, up);
                    }
                }
                else
                {
                    baseRot = Quaternion.identity;
                }
                Quaternion finalRot = baseRot * Quaternion.Euler(rotationOffset);
                placedObjectsCache[i].position = pos + finalRot * positionOffset;
                placedObjectsCache[i].rotation = finalRot;
            }
        }

        void EnsureObjectPool(int needed)
        {
            if (placedObjectsCache == null)
                placedObjectsCache = new Transform[0];

            if (placedObjectsCache.Length >= needed) return;

            var newCache = new Transform[needed];
            for (int i = 0; i < placedObjectsCache.Length; i++)
                newCache[i] = placedObjectsCache[i];

            for (int i = placedObjectsCache.Length; i < needed; i++)
            {
                GameObject go;
                if (objectPrefab != null)
                {
                    go = Instantiate(objectPrefab, transform);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetParent(transform);
                    go.transform.localScale = Vector3.one * 0.05f;
                    var col = go.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
#endif
                go.SetActive(false);
                newCache[i] = go.transform;
            }

            placedObjectsCache = newCache;
        }

        void HideAllPlacedObjects()
        {
            if (placedObjectsCache == null) return;
            for (int i = 0; i < placedObjectsCache.Length; i++)
            {
                if (placedObjectsCache[i] != null)
                    placedObjectsCache[i].gameObject.SetActive(false);
            }
            activePlacedCount = 0;
        }

        // ─────────────────────────────────────────
        //  Cleanup
        // ─────────────────────────────────────────

        void OnDestroy()
        {
            // Pipe 메쉬 정리
            if (pipeMesh != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(pipeMesh);
                else
#endif
                    Destroy(pipeMesh);
            }

            // PlaceObjects 풀 정리
            if (placedObjectsCache != null)
            {
                for (int i = 0; i < placedObjectsCache.Length; i++)
                {
                    if (placedObjectsCache[i] != null)
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                            DestroyImmediate(placedObjectsCache[i].gameObject);
                        else
#endif
                            Destroy(placedObjectsCache[i].gameObject);
                    }
                }
            }
        }

#if UNITY_EDITOR
        [Header("── Gizmo (에디터 전용) ───────────")]
        [Tooltip("Scene 뷰에서 경로 곡선 색상")]
        public Color pathColor = Color.cyan;

        [Tooltip("Scene 뷰 곡선 분할 수")]
        [Range(8, 100)]
        public int gizmoSegments = 48;

        void OnDrawGizmos()
        {
            if (startPoint == null || endPoint == null) return;
            DrawPathGizmo();
        }

        void OnDrawGizmosSelected()
        {
            DrawPathGizmo();
        }

        void DrawPathGizmo()
        {
            if (pathPointsCache == null || pathPointsCache.Length < 2) return;

            float totalLen = cachedTotalLength;

            // ── 경로 곡선 (Arc-Length 기반 균등 분할) ──
            Gizmos.color = pathColor;
            Vector3 prev = PathPlacementModule.EvaluatePath(pathPointsCache, 0f);
            for (int i = 1; i <= gizmoSegments; i++)
            {
                float dist = (i / (float)gizmoSegments) * totalLen;
                float t = PathPlacementModule.DistanceToParameter(arcLengthTable, dist, totalLen);
                Vector3 curr = PathPlacementModule.EvaluatePath(pathPointsCache, t);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }

            // ── 시작(초록)/끝(빨강) 마커 ──
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(pathPointsCache[0], 0.05f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pathPointsCache[pathPointsCache.Length - 1], 0.05f);

            // ── 중간 앵커 마커 (흰색) ──
            if (midAnchors != null)
            {
                Gizmos.color = Color.white;
                for (int i = 0; i < midAnchors.Length; i++)
                {
                    if (midAnchors[i] != null)
                        Gizmos.DrawWireSphere(midAnchors[i].position, 0.04f);
                }
            }

            // ── 배치 위치 + 회전 마커 ──
            if (totalLen < 1e-6f) return;

            // 실제 배치 로직과 동일한 간격 계산
            int count;
            float step;
            if (mode == ConnectorMode.PlaceObjects)
            {
                if (spacingMode == PathPlacementModule.SpacingMode.FixedDistance)
                {
                    count = Mathf.Max(1, Mathf.FloorToInt(totalLen / Mathf.Max(fixedDistance, 0.001f)) + 1);
                    step = fixedDistance;
                }
                else
                {
                    count = maxObjectCount;
                    step = totalLen / Mathf.Max(1, count - 1);
                }
                count = Mathf.Min(count, maxObjectCount);
                if (spacingMode == PathPlacementModule.SpacingMode.EvenDistribute && count == 1)
                    step = 0f;
            }
            else
            {
                // Pipe: 와이어 원형 단면 표시
                count = 8;
                step = totalLen / Mathf.Max(1, count - 1);
            }

            const float arrowLen = 0.1f;
            // 직사각형: 전방(Z) 길고, 좌우(X) 넓고, 상하(Y) 납작
            Vector3 rectSize = new Vector3(arrowLen * 1.2f, arrowLen * 0.3f, arrowLen * 1.5f);

            // up 보간용
            Vector3 gizUpStart = startPoint != null ? startPoint.up : Vector3.up;
            Vector3 gizUpEnd = endPoint != null ? endPoint.up : Vector3.up;

            // PlaceObjects 모드: 스크롤 오프셋 반영
            float gizScrollOffset = (mode == ConnectorMode.PlaceObjects) ? scrollOffset : 0f;

            for (int i = 0; i < count; i++)
            {
                float dist = i * step + gizScrollOffset;
                dist = Mathf.Clamp(dist, 0f, totalLen);
                PathPlacementModule.SamplePath(pathPointsCache, dist, arcLengthTable, totalLen,
                    out Vector3 pos, out Vector3 dir);
                if (dir.sqrMagnitude < 1e-6f) continue;

                Quaternion rot;
                if (is2D)
                {
                    rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                }
                else
                {
                    float t = totalLen > 1e-6f ? dist / totalLen : 0f;
                    Vector3 up = Vector3.Slerp(gizUpStart, gizUpEnd, t);
                    rot = Quaternion.LookRotation(dir, up);
                }

                // PlaceObjects: rotationOffset 반영
                Quaternion finalRot = (mode == ConnectorMode.PlaceObjects)
                    ? rot * Quaternion.Euler(rotationOffset)
                    : rot;

                if (mode == ConnectorMode.Pipe)
                {
                    Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
                    DrawWireCircleGizmo(pos, rot, pipeRadius, 12);
                }
                else if (mode == ConnectorMode.PlaceObjects)
                {
                    // positionOffset 반영
                    Vector3 offsetPos = pos + finalRot * positionOffset;
                    Gizmos.color = Color.yellow;
                    Gizmos.matrix = Matrix4x4.TRS(offsetPos, finalRot, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, rectSize);
                    Gizmos.matrix = Matrix4x4.identity;
                    pos = offsetPos;
                }

                // 전방(Z) = 파랑
                Gizmos.color = new Color(0.2f, 0.4f, 1f);
                Gizmos.DrawRay(pos, finalRot * Vector3.forward * arrowLen);
                // 상방(Y) = 초록
                Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.6f);
                Gizmos.DrawRay(pos, finalRot * Vector3.up * arrowLen * 0.5f);
                // 우측(X) = 빨강
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
                Gizmos.DrawRay(pos, finalRot * Vector3.right * arrowLen * 0.5f);
            }

            // Pipe 모드: 와이어 원 그리기
            void DrawWireCircleGizmo(Vector3 center, Quaternion rotation, float radius, int segments)
            {
                Vector3 prev = center + rotation * new Vector3(radius, 0f, 0f);
                for (int i = 1; i <= segments; i++)
                {
                    float angle = (i / (float)segments) * Mathf.PI * 2f;
                    Vector3 next = center + rotation * new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius, 0f);
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }
            }

            // Pipe 모드: 경로를 따라 외곽선도 표시 (파이프 실루엣)
            if (mode == ConnectorMode.Pipe && !renderMesh)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                int silhouetteSegs = 32;
                // 상하좌우 4방향 외곽선
                for (int axis = 0; axis < 4; axis++)
                {
                    float angle = (axis / 4f) * Mathf.PI * 2f;
                    Vector3 prevSil = Vector3.zero;
                    for (int s = 0; s <= silhouetteSegs; s++)
                    {
                        float d = (s / (float)silhouetteSegs) * totalLen;
                        PathPlacementModule.SamplePath(pathPointsCache, d, arcLengthTable, totalLen,
                            out Vector3 p, out Vector3 dr);
                        if (dr.sqrMagnitude < 1e-6f) continue;

                        float tt = totalLen > 1e-6f ? d / totalLen : 0f;
                        Vector3 u = Vector3.Slerp(gizUpStart, gizUpEnd, tt);
                        Quaternion r = Quaternion.LookRotation(dr, u);

                        Vector3 offset = r * new Vector3(
                            Mathf.Cos(angle) * pipeRadius,
                            Mathf.Sin(angle) * pipeRadius, 0f);
                        Vector3 silPos = p + offset;

                        if (s > 0)
                            Gizmos.DrawLine(prevSil, silPos);
                        prevSil = silPos;
                    }
                }
            }
        }
#endif
    }
}
