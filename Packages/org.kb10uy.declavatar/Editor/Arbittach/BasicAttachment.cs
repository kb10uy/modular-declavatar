using KusakaFactory.Declavatar.Arbittach;

[assembly: ExportArbittachSchema(typeof(BasicAttachment))]
namespace KusakaFactory.Declavatar.Arbittach
{
    [DefineProperty("PropertyX", 1)]
    [DefineProperty("PropertyY", 2)]
    public sealed class BasicAttachment
    {
        [BindValue("PropertyX.0")]
        public string Name { get; set; }

        [BindValue("PropertyY.0")]
        public int Value { get; set; }

        [BindValue("PropertyX.Keyword")]
        public string KeywordValue { get; set; }

        [BindValue("PropertyY.?Maybe")]
        public float MaybeValue { get; set; }
    }
}
