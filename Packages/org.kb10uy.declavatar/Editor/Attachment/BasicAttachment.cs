using Newtonsoft.Json;
using UnityEditor;
using KusakaFactory.Declavatar.Arbittach;
using KusakaFactory.Declavatar.Attachment;

[assembly: ExportsProcessor(typeof(BasicAttachment), "BasicAttachment")]
namespace KusakaFactory.Declavatar.Attachment
{
    public sealed class BasicAttachment : ArbittachProcessor<BasicAttachment, BasicAttachment.Attachment>
    {
        public override void Process(Attachment deserialized, DeclavatarContext context)
        {
            var objectJson = JsonConvert.SerializeObject(deserialized, new JsonSerializerSettings { Formatting = Formatting.Indented });
            EditorUtility.DisplayDialog($"BasicAttachmentProcessor", objectJson, "OK");
        }

        [DefineProperty("PropertyX", 1)]
        [DefineProperty("PropertyY", 2)]
        public sealed class Attachment
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
}
