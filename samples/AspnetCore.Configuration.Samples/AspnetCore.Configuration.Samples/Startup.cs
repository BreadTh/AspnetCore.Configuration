using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using BreadTh.AspNet.Configuration;

namespace AspnetCore.Configuration.Samples
{
    public class Startup : StandardStartup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment) : base(environment, configuration) { }

        override protected void SpecificConfigureServices(IServiceCollection serviceCollection)
        {
        }

        override protected void SpecificConfigure(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider)
        {
        }
    }
}
