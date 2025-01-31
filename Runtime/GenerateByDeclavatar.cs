using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

namespace KusakaFactory.Declavatar.Runtime
{
    [Icon("Packages/org.kb10uy.declavatar/Resources/Images/declavatar-logo.png")]
    [AddComponentMenu("Declavatar/Attach Declavatar Script")]
    public class GenerateByDeclavatar : MonoBehaviour, IEditorOnly
    {
        public DeclarationFormat Format = DeclarationFormat.SExpression;
        public TextAsset Definition;
        public DeclavatarExternalAssets[] ExternalAssets;
        public string[] Symbols;

        public GameObject DeclarationRoot;
        public GameObject InstallTarget;
        public bool GenerateMenuInstaller = false;

        public enum DeclarationFormat : uint
        {
            SExpression = 1,
            Lua = 2,
        }

        public class CompiledDeclavatar : MonoBehaviour, IEditorOnly
        {
            public Avatar CompiledAvatar;
            public Dictionary<string, (string, Object)> ExternalAssets;

            public GameObject DeclarationRoot;
            public GameObject MenuInstallTarget;
            public bool CreateMenuInstallerComponent = false;
        }
    }
}
