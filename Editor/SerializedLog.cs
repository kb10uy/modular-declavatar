using System.Collections.Generic;

namespace KusakaFactory.Declavatar
{
    internal class SerializedLog
    {
        public string Severity { get; set; }
        public string Kind { get; set; }
        public List<string> Args { get; set; }
        public List<string> Context { get; set; }
    }
}
