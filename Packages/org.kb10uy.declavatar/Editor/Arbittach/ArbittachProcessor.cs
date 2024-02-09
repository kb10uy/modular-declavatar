using nadena.dev.ndmf;

namespace KusakaFactory.Declavatar.Arbittach
{
    /// <summary>
    /// Base class of arbitrary attachment processing.
    /// </summary>
    /// <typeparam name="TSelf">Specify mplementing type itself.</typeparam>
    /// <typeparam name="TAttachment">Type of attachment data.</typeparam>
    public abstract class ArbittachProcessor<TSelf, TAttachment> : IErasedProcessor
    where TSelf : ArbittachProcessor<TSelf, TAttachment>, new()
    {
        /// <summary>
        /// Analyzed definition of attachment type.
        /// It also contains serializable attachment schema.
        /// </summary>
        protected internal AttachmentDefinition Definition { get; private set; }

        /// <summary>
        /// Processes a single attachment.
        /// </summary>
        /// <param name="deserialized">Deserialized attachment object from avatar definition.</param>
        /// <param name="context">Current BuildContext from NDMF.</param>
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
