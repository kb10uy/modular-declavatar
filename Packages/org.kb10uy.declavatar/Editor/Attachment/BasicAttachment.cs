using Newtonsoft.Json;
using UnityEditor;
using nadena.dev.ndmf;
using KusakaFactory.Declavatar.Arbittach;
using KusakaFactory.Declavatar.Attachment;

[assembly: ExportsProcessor(typeof(BasicAttachmentProcessor))]
namespace KusakaFactory.Declavatar.Attachment
{
    public sealed class BasicAttachmentProcessor : ArbittachProcessor<BasicAttachmentProcessor, BasicAttachment>
    {
        public override void Process(BasicAttachment deserialized, BuildContext context)
        {
            var objectJson = JsonConvert.SerializeObject(deserialized, new JsonSerializerSettings { Formatting = Formatting.Indented });
            EditorUtility.DisplayDialog($"BasicAttachmentProcessor", objectJson, "OK");
        }
    }

    [DefineProperty("PropertyX", 1)]
    [DefineProperty("PropertyY", 2)]
    public sealed class BasicAttachment
    {
        [BindValue("PropertyX.0")]
        public string Name { get; set; }

        [BindValue("PropertyY.0")]
        public int Value { get; set; }

        [BindValue("PropertyY.1")]
        public bool Flag { get; set; }

        [BindValue("PropertyX.?Keyword")]
        public string KeywordValue { get; set; }

        [BindValue("PropertyY.?Maybe")]
        public float MaybeValue { get; set; }
    }
}
