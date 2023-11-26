using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Declavatar.Runtime
{
    public class GenerateByDeclavatar : MonoBehaviour, IEditorOnly
    {
        public TextAsset Definition;
        public ExternalAsset[] ExternalAssets;

        public GameObject InstallTarget;
    }
}
