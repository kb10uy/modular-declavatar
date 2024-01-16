using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
using Avatar = KusakaFactory.Declavatar.Runtime.Data.Avatar;

namespace KusakaFactory.Declavatar.Runtime
{
    public class GenerateByDeclavatar : MonoBehaviour, IEditorOnly
    {
        public DeclarationFormat Format = DeclarationFormat.SExpression;
        public TextAsset Definition;
        public ExternalAsset[] ExternalAssets;
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
            public Dictionary<string, Material> ExternalMaterials;
            public Dictionary<string, AnimationClip> ExternalAnimationClips;

            public GameObject DeclarationRoot;
            public GameObject MenuInstallTarget;
            public bool CreateMenuInstallerComponent = false;
        }
    }
}
