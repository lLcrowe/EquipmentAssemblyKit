using lLCroweTool.EquipmentAssemblyKit;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.EditorOnly
{
    /// <summary>
    /// .equipment 확장자 파일을 EquipmentInfo SO로 임포트한다.
    /// GUID 기반 직렬화로 에셋 참조를 세션 간 보존한다.
    /// </summary>
    [ScriptedImporter(4, "equipment")]
    public class EquipmentScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string json = System.IO.File.ReadAllText(ctx.assetPath);
            var equipmentInfo = ScriptableObject.CreateInstance<EquipmentInfo>();

            if (!string.IsNullOrEmpty(json))
            {
                EquipmentGuidSerializer.FromJsonOverwrite(json, equipmentInfo);
            }

            equipmentInfo.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("main", equipmentInfo);
        }
    }
}
