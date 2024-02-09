using nadena.dev.ndmf;

namespace KusakaFactory.Declavatar.Arbittach
{
    public abstract class ArbittachProcessor<TSelf, TAttachment> : IErasedProcessor
    where TSelf : ArbittachProcessor<TSelf, TAttachment>, new()
    {
        protected internal AttachmentDefinition Definition { get; private set; }

        public abstract void Process(TAttachment deserialized, BuildContext context);

        #region Internal

        AttachmentDefinition IErasedProcessor.Definition => Definition;

        void IErasedProcessor.Configure(AttachmentDefinition definition)
        {
            Definition = definition;
        }

        void IErasedProcessor.ProcessErased(object deserializedErased, BuildContext context)
        {
            var deserialized = (TAttachment)deserializedErased;
            Process(deserialized, context);
        }

        #endregion
    }

    internal interface IErasedProcessor
    {
        internal AttachmentDefinition Definition { get; }
        internal void Configure(AttachmentDefinition definition);
        internal void ProcessErased(object deserializedErased, BuildContext context);
    }
}
