using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using KusakaFactory.Declavatar.Arbittach;

namespace KusakaFactory.Declavatar.Processor
{
    internal sealed class ExecuteArbittachPass : IDeclavatarPass
    {
        private Dictionary<string, AttachmentDefinition> _attachmentDefinitions;

        internal ExecuteArbittachPass(Dictionary<string, AttachmentDefinition> attachmentDefinitions)
        {
            _attachmentDefinitions = attachmentDefinitions;
        }

        public void Execute(DeclavatarContext context)
        {
            var rawAttachmentGroups = context.AvatarDeclaration.Attachments;
            foreach (var group in rawAttachmentGroups)
            {
                foreach (var attachment in group.Attachments)
                {
                    var definition = _attachmentDefinitions[attachment.Name];
                    var deserializedObject = definition.Deserialize(attachment, context);
                    var schemaJson = JsonConvert.SerializeObject(deserializedObject, new JsonSerializerSettings { Formatting = Formatting.Indented });
                    EditorUtility.DisplayDialog($"Arbittach for {group.Target}", schemaJson, "OK");
                }
            }
        }
    }
}
