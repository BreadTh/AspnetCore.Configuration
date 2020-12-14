#pragma warning disable CS0649

namespace BreadTh.AspNet.Configuration.Core
{
    public class StandardConfiguration
    {
        public bool UseDeveloperExceptionPage { get; set; }
        public StandardConfigurationHttp Http { get; set; }
        public StandardConfigurationController Controller { get; set; }
    }
}
#pragma warning restore CS0649
