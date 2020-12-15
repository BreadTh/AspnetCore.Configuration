using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using BreadTh.AspNet.Configuration;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AspnetCore.Configuration.Samples
{
    public class Startup : StandardStartup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment) : base(environment, configuration) 
        {
        }

        override protected void SpecificConfigureServices(IServiceCollection serviceCollection)
        {
        }

        override protected void EarlyBuild(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider)
        {
        }

        override protected void LateBuild(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider)
        {
        }

        protected override async Task OnUnhandledException(Exception exception, HttpContext httpContext)
        {

        }
    }
}
