using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;

namespace KusakaFactory.Declavatar.EditorExtension
{
    [ScriptedImporter(1, "declisp")]
    public sealed class SExpressionImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var kdlBytes = File.ReadAllBytes(ctx.assetPath);
            var convertedText = Encoding.UTF8.GetString(kdlBytes);
            var textAsset = new TextAsset(convertedText);
            ctx.AddObjectToAsset("MainAsset", textAsset);
            ctx.SetMainObject(textAsset);
        }
    }
}
