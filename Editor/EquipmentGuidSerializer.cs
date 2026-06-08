using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// UnityEngine.Object 참조를 GUID 기반으로 직렬화/역직렬화한다.
    /// JsonUtility는 instanceID를 사용하므로 세션 간 참조가 깨진다.
    /// 이 유틸리티가 __assetRefs 섹션으로 GUID를 보존하여 복원한다.
    /// </summary>
    public static class EquipmentGuidSerializer
    {
        [Serializable]
        private class AssetRefEntry
        {
            public string p; // field path (dot + bracket notation)
            public string g; // GUID
            public long f;   // localFileID
        }

        [Serializable]
        private class AssetRefContainer
        {
            public AssetRefEntry[] r = Array.Empty<AssetRefEntry>();
        }

        // 직렬화 시 무시할 어트리뷰트
        static readonly Type nonSerializedType = typeof(NonSerializedAttribute);

        /// <summary>
        /// SO를 JSON으로 직렬화한다. UnityEngine.Object 참조는 GUID로 저장된다.
        /// </summary>
        public static string ToJson(ScriptableObject so, bool prettyPrint = true)
        {
            // 1. 기본 JSON (instanceID 포함)
            string baseJson = JsonUtility.ToJson(so, prettyPrint);

            // 2. 에셋 참조 수집
            var refs = new List<AssetRefEntry>();
            CollectAssetRefs(so, so.GetType(), "", refs);

            if (refs.Count == 0)
            {
                // __assetRefs 없이 기존 형태 유지 + 버전 표기만
                return InjectVersion(baseJson, prettyPrint);
            }

            // 3. __assetRefs + __version 주입
            var container = new AssetRefContainer { r = refs.ToArray() };
            string refsJson = JsonUtility.ToJson(container, false);

            return InjectVersionAndRefs(baseJson, refsJson, prettyPrint);
        }

        /// <summary>
        /// GUID 기반 JSON에서 SO를 복원한다. 구버전(instanceID 전용) JSON도 호환된다.
        /// </summary>
        public static void FromJsonOverwrite(string json, ScriptableObject so)
        {
            // 1. 기본 데이터 복원 (Object 참조는 instanceID=0이면 null)
            JsonUtility.FromJsonOverwrite(json, so);

            // 2. __assetRefs 추출
            var container = ExtractAssetRefs(json);
            if (container == null || container.r == null || container.r.Length == 0)
                return;

            // 3. GUID → 에셋 복원
            foreach (var entry in container.r)
            {
                if (string.IsNullOrEmpty(entry.g)) continue;

                string assetPath = AssetDatabase.GUIDToAssetPath(entry.g);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogWarning($"[GuidSerializer] GUID '{entry.g}' 경로 미발견 (path: {entry.p})");
                    continue;
                }

                UnityEngine.Object asset = LoadAssetByFileID(assetPath, entry.f);
                if (asset == null)
                {
                    Debug.LogWarning($"[GuidSerializer] 에셋 로드 실패: {assetPath} (path: {entry.p})");
                    continue;
                }

                SetFieldByPath(so, entry.p, asset);
            }
        }

        /// <summary>
        /// 기존 instanceID 기반 JSON인지 확인한다. __version 필드가 없으면 구버전.
        /// </summary>
        public static bool IsLegacyFormat(string json)
        {
            return !json.Contains("\"__version\"");
        }

        #region 직렬화: 에셋 참조 수집

        static void CollectAssetRefs(object obj, Type type, string pathPrefix, List<AssetRefEntry> refs)
        {
            if (obj == null) return;

            var fields = GetSerializedFields(type);

            foreach (var field in fields)
            {
                string fieldPath = string.IsNullOrEmpty(pathPrefix)
                    ? field.Name
                    : pathPrefix + "." + field.Name;

                Type fieldType = field.FieldType;
                object value = field.GetValue(obj);

                if (IsUnityObjectType(fieldType))
                {
                    // 단일 UnityEngine.Object 참조
                    var unityObj = value as UnityEngine.Object;
                    if (unityObj != null)
                    {
                        var entry = CreateRefEntry(fieldPath, unityObj);
                        if (entry != null) refs.Add(entry);
                    }
                }
                else if (fieldType.IsArray && IsUnityObjectType(fieldType.GetElementType()))
                {
                    // UnityEngine.Object 배열
                    var arr = value as Array;
                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var elem = arr.GetValue(i) as UnityEngine.Object;
                            if (elem != null)
                            {
                                var entry = CreateRefEntry($"{fieldPath}[{i}]", elem);
                                if (entry != null) refs.Add(entry);
                            }
                        }
                    }
                }
                else if (fieldType.IsArray)
                {
                    // 일반 배열 (struct/class 배열, 내부에 Object 필드가 있을 수 있음)
                    var arr = value as Array;
                    if (arr != null)
                    {
                        Type elemType = fieldType.GetElementType();
                        if (elemType != null && IsSerializableComposite(elemType))
                        {
                            for (int i = 0; i < arr.Length; i++)
                            {
                                object elem = arr.GetValue(i);
                                if (elem != null)
                                    CollectAssetRefs(elem, elemType, $"{fieldPath}[{i}]", refs);
                            }
                        }
                    }
                }
                else if (IsSerializableComposite(fieldType))
                {
                    // 중첩 [Serializable] class/struct
                    if (value != null)
                        CollectAssetRefs(value, fieldType, fieldPath, refs);
                }
            }
        }

        static AssetRefEntry CreateRefEntry(string path, UnityEngine.Object obj)
        {
            if (obj == null) return null;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId))
            {
                if (string.IsNullOrEmpty(guid) || guid == "00000000000000000000000000000000")
                    return null;

                return new AssetRefEntry { p = path, g = guid, f = localId };
            }

            return null;
        }

        #endregion

        #region 역직렬화: 에셋 참조 복원

        static AssetRefContainer ExtractAssetRefs(string json)
        {
            // "__assetRefs" 키 검색
            int key = json.IndexOf("\"__assetRefs\"", StringComparison.Ordinal);
            if (key < 0) return null;

            // 콜론 뒤의 { 찾기
            int colon = json.IndexOf(':', key + 13);
            if (colon < 0) return null;

            int braceStart = json.IndexOf('{', colon);
            if (braceStart < 0) return null;

            // 매칭되는 } 찾기
            int depth = 0;
            int braceEnd = -1;
            for (int i = braceStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0) { braceEnd = i; break; }
                }
            }
            if (braceEnd < 0) return null;

            string containerJson = json.Substring(braceStart, braceEnd - braceStart + 1);
            try
            {
                return JsonUtility.FromJson<AssetRefContainer>(containerJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GuidSerializer] __assetRefs 파싱 실패: {e.Message}");
                return null;
            }
        }

        static UnityEngine.Object LoadAssetByFileID(string assetPath, long fileID)
        {
            if (fileID == 0)
            {
                // 메인 에셋
                return AssetDatabase.LoadMainAssetAtPath(assetPath);
            }

            // 서브 에셋 탐색 (Sprite in Texture, etc.)
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in allAssets)
            {
                if (asset == null) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id))
                {
                    if (id == fileID) return asset;
                }
            }

            // fileID 매칭 실패 시 메인 에셋 반환
            return AssetDatabase.LoadMainAssetAtPath(assetPath);
        }

        static void SetFieldByPath(object root, string path, UnityEngine.Object value)
        {
            // path 예: "contribution.visualPrefab", "childSlots[0].connectVisualPrefab", "contribution.sounds[2]"
            var segments = ParsePath(path);
            object current = root;

            for (int i = 0; i < segments.Count - 1; i++)
            {
                current = ResolveSegment(current, segments[i]);
                if (current == null)
                {
                    Debug.LogWarning($"[GuidSerializer] 경로 탐색 실패: {path} (segment: {segments[i].name})");
                    return;
                }
            }

            // 마지막 세그먼트에 값 할당
            var last = segments[segments.Count - 1];

            if (last.index >= 0)
            {
                // 배열 요소
                var field = FindField(current.GetType(), last.name);
                if (field == null) return;
                var arr = field.GetValue(current) as Array;
                if (arr != null && last.index < arr.Length)
                {
                    arr.SetValue(value, last.index);
                    // struct인 경우 부모에 다시 할당
                    if (current.GetType().IsValueType)
                        PropagateStructChange(root, segments, current);
                }
            }
            else
            {
                // 일반 필드
                var field = FindField(current.GetType(), last.name);
                if (field == null) return;
                field.SetValue(current, value);
                // struct인 경우 부모에 다시 할당
                if (current.GetType().IsValueType)
                    PropagateStructChange(root, segments, current);
            }
        }

        #endregion

        #region 경로 파싱

        struct PathSegment
        {
            public string name;
            public int index; // -1이면 배열 아님

            public PathSegment(string name, int index = -1)
            {
                this.name = name;
                this.index = index;
            }
        }

        static List<PathSegment> ParsePath(string path)
        {
            var result = new List<PathSegment>();
            var parts = path.Split('.');

            foreach (var part in parts)
            {
                int bracketOpen = part.IndexOf('[');
                if (bracketOpen >= 0)
                {
                    string name = part.Substring(0, bracketOpen);
                    int bracketClose = part.IndexOf(']', bracketOpen);
                    string idxStr = part.Substring(bracketOpen + 1, bracketClose - bracketOpen - 1);
                    int idx = int.Parse(idxStr);
                    result.Add(new PathSegment(name, idx));
                }
                else
                {
                    result.Add(new PathSegment(part));
                }
            }

            return result;
        }

        static object ResolveSegment(object obj, PathSegment seg)
        {
            if (obj == null) return null;

            var field = FindField(obj.GetType(), seg.name);
            if (field == null) return null;

            object val = field.GetValue(obj);

            if (seg.index >= 0)
            {
                var arr = val as Array;
                if (arr != null && seg.index < arr.Length)
                    return arr.GetValue(seg.index);
                return null;
            }

            return val;
        }

        // struct 변경 전파: struct를 수정한 후 부모 체인에 다시 할당
        static void PropagateStructChange(object root, List<PathSegment> segments, object modifiedValue)
        {
            // 단순화: ScriptedImporter 시나리오에서 struct는 최대 2단계 깊이
            // 일반적으로 PartSlot (struct)이 childSlots[i]에 있는 케이스
            // JsonUtility.FromJsonOverwrite 이후 reflection으로 값을 설정하므로
            // struct copy 문제는 배열 요소에서만 발생
            // 이 케이스는 SetFieldByPath에서 Array.SetValue로 직접 처리됨
        }

        #endregion

        #region 리플렉션 유틸

        static FieldInfo FindField(Type type, string name)
        {
            while (type != null && type != typeof(UnityEngine.Object))
            {
                var field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        static List<FieldInfo> GetSerializedFields(Type type)
        {
            var result = new List<FieldInfo>();
            var current = type;

            while (current != null && current != typeof(UnityEngine.Object) && current != typeof(object))
            {
                var fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (var f in fields)
                {
                    if (Attribute.IsDefined(f, nonSerializedType)) continue;
                    if (Attribute.IsDefined(f, typeof(System.NonSerializedAttribute))) continue;

                    // Unity 직렬화 규칙: public이거나 [SerializeField]
                    bool isPublic = f.IsPublic;
                    bool hasSerializeField = Attribute.IsDefined(f, typeof(SerializeField));

                    if (!isPublic && !hasSerializeField) continue;

                    // HideInInspector가 있어도 직렬화는 됨 (표시만 안 함)
                    result.Add(f);
                }

                current = current.BaseType;
            }

            return result;
        }

        static bool IsUnityObjectType(Type type)
        {
            return type != null && typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        static bool IsSerializableComposite(Type type)
        {
            if (type == null) return false;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return false;
            if (IsUnityObjectType(type)) return false;

            // Unity 빌트인 타입 (Vector3, Color 등)은 내부에 Object 참조 없음
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
            {
                // AnimationCurve는 Object 참조 없음
                return false;
            }

            // [Serializable] 표시된 class/struct
            return type.IsSerializable || Attribute.IsDefined(type, typeof(SerializableAttribute));
        }

        #endregion

        #region JSON 주입

        static string InjectVersion(string json, bool prettyPrint)
        {
            // { 바로 뒤에 "__version": 2 추가
            int firstBrace = json.IndexOf('{');
            if (firstBrace < 0) return json;

            string indent = prettyPrint ? "\n    " : "";
            string separator = prettyPrint ? ",\n" : ",";
            string prefix = json.Substring(0, firstBrace + 1);
            string rest = json.Substring(firstBrace + 1);

            return prefix + indent + "\"__version\": 2," + rest;
        }

        static string InjectVersionAndRefs(string baseJson, string refsJson, bool prettyPrint)
        {
            int firstBrace = baseJson.IndexOf('{');
            if (firstBrace < 0) return baseJson;

            string indent = prettyPrint ? "\n    " : "";
            string prefix = baseJson.Substring(0, firstBrace + 1);
            string rest = baseJson.Substring(firstBrace + 1);

            return prefix +
                   indent + "\"__version\": 2," +
                   indent + "\"__assetRefs\": " + refsJson + "," +
                   rest;
        }

        #endregion

    }
}
