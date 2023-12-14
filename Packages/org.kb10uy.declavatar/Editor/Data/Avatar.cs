using System.Collections.Generic;

namespace KusakaFactory.Declavatar.Data
{
    public sealed class Avatar
    {
        public string Name { get; set; }
        public List<Parameter> Parameters { get; set; }
        public List<Asset> Assets { get; set; }
        public List<Layer> FxController { get; set; }
        public List<MenuItem> MenuItems { get; set; }
    }
}
