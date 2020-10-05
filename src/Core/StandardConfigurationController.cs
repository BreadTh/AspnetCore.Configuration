#pragma warning disable CS0649
namespace BreadTh.AspNet.Configuration.Core
{
    public class StandardConfigurationController
    {
        public bool IsFrontend { get; set; }
        public bool DoesAnyCookieRequireConsent { get; set; }
        public int SessionIdleTimeoutInMinutes { get; set; }
        public bool UseDefaultRoutes { get; set; }
        public StandardConfigurationRoute[] AdditionalRoutes { get; set; }
    }
}
#pragma warning restore CS0649
