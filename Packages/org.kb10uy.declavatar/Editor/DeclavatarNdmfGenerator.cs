#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using nadena.dev.ndmf;
using AnimatorAsCode.V1.NDMFProcessor;
using KusakaFactory.Declavatar;
using KusakaFactory.Declavatar.Runtime;

[assembly: ExportsPlugin(typeof(DeclavatarNdmfGenerator))]
[assembly: ExportsPlugin(typeof(DeclavatarComponentRemover))]
namespace KusakaFactory.Declavatar
{
    public class DeclavatarNdmfGenerator : AacPlugin<GenerateByDeclavatar>
    {
        protected override AacPluginOutput Execute()
        {
            // Skip if definition is empty
            if (my.Definition == null) return AacPluginOutput.Regular();

            // Compile
            string definitionJson;
            using (var declavatarPlugin = new Plugin())
            {
                declavatarPlugin.Reset();
                if (!declavatarPlugin.Compile(my.Definition.text))
                {
                    var errors = declavatarPlugin.FetchErrors();
                    var errorMessage = $"Declavatar definition error:\n{string.Join("\n", errors.Select((e) => e.Item2))}";
                    throw new DeclavatarDeclarationException(errorMessage);
                }

                definitionJson = declavatarPlugin.GetAvatarJson();
            }

            var definition = JsonConvert.DeserializeObject<Avatar>(
                definitionJson,
                new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new SnakeCaseNamingStrategy(),
                    }
                }
            );
            var externalAssets = my.ExternalAssets.Where((ea) => ea != null).ToList();
            Debug.Log($"Declavatar: definition '{definition.Name}' compiled");

            var declavatar = new NonDestructiveDeclavatar(my.gameObject, aac, definition, externalAssets);
            declavatar.Execute();
            return AacPluginOutput.Regular();
        }
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
            foreach (var component in components) Object.DestroyImmediate(component);
        }
    }
}
#endif
