using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using nadena.dev.ndmf;
using AnimatorAsCode.V1.NDMFProcessor;
using KusakaFactory.Declavatar;
using KusakaFactory.Declavatar.EditorExtension;
using KusakaFactory.Declavatar.Runtime;
using nadena.dev.ndmf.localization;
using UnityEditor;


[assembly: ExportsPlugin(typeof(DeclavatarNdmfGenerator))]
[assembly: ExportsPlugin(typeof(DeclavatarComponentRemover))]
namespace KusakaFactory.Declavatar
{
    public class DeclavatarNdmfGenerator : AacPlugin<GenerateByDeclavatar>
    {
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            }
        };

        protected override AacPluginOutput Execute()
        {
            // Skip if definition is empty
            if (my.Definition == null) return AacPluginOutput.Regular();

            // Compile
            var localizer = ConstructLocalizer();
            string definitionJson;
            using (var declavatarPlugin = new Plugin())
            {
                declavatarPlugin.Reset();

                var config = Configuration.LoadEditorUserSettings();
                foreach (var path in config.LibraryRelativePath)
                {
                    var p = path.Trim();
                    if (!string.IsNullOrEmpty(p)) declavatarPlugin.AddLibraryPath(ConcatenateProjectRelativePath(p));
                }

                if (!declavatarPlugin.Compile(my.Definition.text, (FormatKind)(uint)my.Format))
                {
                    ReportLogsForNdmf(localizer, declavatarPlugin.FetchErrors());
                    return AacPluginOutput.Regular();
                }

                definitionJson = declavatarPlugin.GetAvatarJson();
            }

            var definition = JsonConvert.DeserializeObject<Data.Avatar>(definitionJson, _serializerSettings);
            var externalAssets = my.ExternalAssets.Where((ea) => ea != null).ToList();
            Debug.Log($"Declavatar: definition '{definition.Name}' compiled");

            var declavatar = new NonDestructiveDeclavatar(
                my.DeclarationRoot != null ? my.DeclarationRoot : my.gameObject,
                my.InstallTarget,
                localizer,
                aac,
                definition,
                externalAssets
            );
            declavatar.Execute(my.GenerateMenuInstaller);
            return AacPluginOutput.Regular();
        }

        private static string ConcatenateProjectRelativePath(string relativePath)
        {
            var assetsPath = Application.dataPath;
            var projectPath = Path.GetDirectoryName(assetsPath);
            return Path.Combine(projectPath, relativePath);
        }

        private static void ReportLogsForNdmf(Localizer localizer, List<string> logJsons)
        {
            foreach (var logJson in logJsons)
            {
                var serializedLog = JsonConvert.DeserializeObject<Data.SerializedLog>(logJson, _serializerSettings);
                var severity = serializedLog.Severity switch
                {
                    "Information" => ErrorSeverity.Information,
                    "Warning" => ErrorSeverity.NonFatal,
                    "Error" => ErrorSeverity.Error,
                    _ => throw new DeclavatarInternalException("unknown severity"),
                };
                ErrorReport.ReportError(localizer, severity, serializedLog.Kind, serializedLog.Args.ToArray());
            }
        }

        #region Localization

        private static Localizer ConstructLocalizer()
        {
            var localizations = new List<(string, Func<string, string>)>
            {
                ("en-us", CreateLocalizerFunc("en-us")),
                ("ja-jp", CreateLocalizerFunc("ja-jp")),
            };
            return new Localizer("en-us", () => localizations);
        }

        private static Func<string, string> CreateLocalizerFunc(string locale)
        {
            var coreLocalization = Plugin.GetLogLocalization(locale);
            var runtimeLocalization = GetRuntimeLocalization(locale);

            var dictionary = new Dictionary<string, string>(coreLocalization.Union(runtimeLocalization));
            return (key) =>
            {
                return dictionary.TryGetValue(key, out var value) ? value : null;
            };
        }

        private static Dictionary<string, string> GetRuntimeLocalization(string locale)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"Packages/org.kb10uy.declavatar/Editor/Localization/{locale}.json");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
        }

        #endregion
    }

    public class DeclavatarComponentRemover : Plugin<DeclavatarComponentRemover>
    {
        public override string DisplayName => "Declavatar Component Remover";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("Remove Declavatar Components", Execute);
        }

        private void Execute(BuildContext ctx)
        {
            var rootObject = ctx.AvatarRootObject;
            var components = rootObject.GetComponentsInChildren<GenerateByDeclavatar>();
            foreach (var component in components) UnityEngine.Object.DestroyImmediate(component);
        }
    }
}
