using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Declavatar.Runtime;

namespace KusakaFactory.Declavatar
{
    [ParameterProviderFor(typeof(GenerateByDeclavatar))]
    internal sealed class DeclavatarParameterProvider : IParameterProvider
    {
        // private DeclavatarCompileService _compileService;
        private GenerateByDeclavatar _component;

        public DeclavatarParameterProvider(GenerateByDeclavatar component)
        {
            _component = component;
            // _compileService = DeclavatarCompileService.Create();
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            /*
            var symbols = _component.Symbols.ToHashSet();
            var (avatar, logs) = _compileService.CompileDeclaration(
                _component.Definition.text,
                (DeclavatarFormat)_component.Format,
                symbols,
                new Dictionary<string, string>()
            );
            if (avatar == null) return new ProvidedParameter[] { };

            Debug.Log(avatar.Parameters.Count);
            return avatar.Parameters.Select((p) => new ProvidedParameter(
                p.Name,
                ParameterNamespace.Animator,
                _component,
                DeclavatarNdmfPlugin.Instance,
                p.ValueType.Type switch
                {
                    "Int" => AnimatorControllerParameterType.Int,
                    "Bool" => AnimatorControllerParameterType.Bool,
                    "Float" => AnimatorControllerParameterType.Float,
                    _ => AnimatorControllerParameterType.Int,
                }
            ));
            */
            return new ProvidedParameter[] { };
        }
    }
}