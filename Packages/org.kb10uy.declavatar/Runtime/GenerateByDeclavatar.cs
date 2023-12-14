using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Declavatar.Runtime
{
    public class GenerateByDeclavatar : MonoBehaviour, IEditorOnly
    {
        public DeclarationFormat Format = DeclarationFormat.SExpression;
        public TextAsset Definition;
        public ExternalAsset[] ExternalAssets;

        public GameObject DeclarationRoot;
        public GameObject InstallTarget;

        public enum DeclarationFormat : uint
        {
            SExpression = 1,
            Lua = 2,
        }
    }
}
