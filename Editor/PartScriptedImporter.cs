using lLCroweTool.EquipmentAssemblyKit;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// .part 확장자 파일을 PartInfo SO로 임포트한다.
    /// GUID 기반 직렬화로 에셋 참조를 세션 간 보존한다.
    /// </summary>
    [ScriptedImporter(4, "part")]
    public class PartScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string json = System.IO.File.ReadAllText(ctx.assetPath);
            var partInfo = ScriptableObject.CreateInstance<PartInfo>();

            if (!string.IsNullOrEmpty(json))
            {
                EquipmentGuidSerializer.FromJsonOverwrite(json, partInfo);
            }

            partInfo.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("main", partInfo);
        }
    }
}
