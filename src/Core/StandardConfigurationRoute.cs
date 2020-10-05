#pragma warning disable CS0649
namespace BreadTh.AspNet.Configuration.Core
{
    public class StandardConfigurationRoute 
    {
        public string Name { get; set; }
        public string Pattern { get; set; }
        public StandardConfigurationRouteDefaults Defaults { get; set; }
    }
}
#pragma warning restore CS0649
