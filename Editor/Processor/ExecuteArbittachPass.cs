using System.Collections.Generic;
using KusakaFactory.Declavatar.Arbittach;

namespace KusakaFactory.Declavatar.Processor
{
    internal sealed class ExecuteArbittachPass : IDeclavatarPass
    {
        private readonly IReadOnlyDictionary<string, IErasedProcessor> _attachmentDefinitions;

        internal ExecuteArbittachPass(IReadOnlyDictionary<string, IErasedProcessor> attachmentDefinitions)
        {
            _attachmentDefinitions = attachmentDefinitions;
        }

        public void Execute(DeclavatarContext context)
        {
            foreach (var rawAttachment in context.AvatarDeclaration.Attachments)
            {
                var processor = _attachmentDefinitions[rawAttachment.Name];
                var deserializedObject = processor.Definition.Deserialize(rawAttachment, context);
                processor.ProcessErased(deserializedObject, context);
            }
        }
    }
}
