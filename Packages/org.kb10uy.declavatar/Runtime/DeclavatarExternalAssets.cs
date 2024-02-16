using System;
using UnityEngine;

namespace KusakaFactory.Declavatar.Runtime
{
    [CreateAssetMenu(fileName = "ExternalAssets", menuName = "Declavatar/External Assets")]
    public sealed class DeclavatarExternalAssets : ScriptableObject
    {
        public Entry[] Entries;

        [Serializable]
        public sealed class Entry
        {
            public string Type = "Material";
            public string Key;
            public UnityEngine.Object Asset;
        }
    }
}
