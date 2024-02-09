using System.Collections.Generic;

namespace KusakaFactory.Declavatar
{
    internal class SerializedLog
    {
        internal string Severity { get; set; }
        internal string Kind { get; set; }
        internal List<string> Args { get; set; }
        internal List<string> Context { get; set; }
    }
}
