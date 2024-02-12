using System.Linq;
using nadena.dev.modular_avatar.core;

namespace KusakaFactory.Declavatar.Processor
{
    internal sealed class GenerateMenuPass : IDeclavatarPass
    {
        public void Execute(DeclavatarContext context)
        {
            // MA Parameters modifies itself and child GameObject
            // It must be on _rootGameObject
            var parametersComponent =
                context.DeclarationRoot.GetComponent<ModularAvatarParameters>() ??
                context.DeclarationRoot.AddComponent<ModularAvatarParameters>();

            var newParameters = context
                .AvatarDeclaration
                .Parameters
                .Select((pd) => new ParameterConfig
                {
                    nameOrPrefix = pd.Name,
                    internalParameter = pd.Unique,
                    hasExplicitDefaultValue = pd.ExplicitDefault,
                    defaultValue = pd.ValueType.ConvertToVRCParameterValue(),
                    saved = pd.Scope.Save ?? false,
                    syncType = pd.ConvertToMASyncType(),
                    localOnly = pd.Scope.Type != "Synced",
                    isPrefix = false, // TODO: PhysBones prefix
                });
            parametersComponent.parameters.AddRange(newParameters);
        }
    }
}
