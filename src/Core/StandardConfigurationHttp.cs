#pragma warning disable CS0649
namespace BreadTh.AspNet.Configuration.Core
{
    public class StandardConfigurationHttp
    {
        public int HttpPort { get; set; }
        public int HttpsPort { get; set; }
        public bool HttpsEnabled { get; set; }
        public string MainPublicDomain { get; set; }
        public string[] AlternativePublicDomains { get; set; }
        public StandardConfigurationCsrInfo CsrInfo { get; set; }
    }
}
#pragma warning restore CS0649
