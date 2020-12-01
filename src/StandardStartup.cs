using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using FluffySpoon.AspNet.LetsEncrypt;
using FluffySpoon.AspNet.LetsEncrypt.Certes;

using BreadTh.AspNet.Configuration.Core;

namespace BreadTh.AspNet.Configuration
{
    public abstract class StandardStartup
    {
        readonly protected IWebHostEnvironment _environment;
        readonly protected IConfiguration _configuration;
        readonly StandardConfiguration _standardConfiguration;

        protected StandardStartup(IWebHostEnvironment environment, IConfiguration configuration) 
        {
            _environment = environment;
            _configuration = configuration;
            _standardConfiguration = _configuration.GetSection("StandardConfiguration").Get<StandardConfiguration>();
        }

        protected abstract void SpecificConfigureServices(IServiceCollection serviceCollection);
        protected abstract void EarlyBuild(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider);
        protected abstract void LateBuild(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider);

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            PrintEnvironment();
            ConfigureServiceHttps(serviceCollection);
            ConfigureServicesControllers(serviceCollection);

            serviceCollection.AddLogging();
            serviceCollection.AddRouting();

            IMvcBuilder mvcBuilder = serviceCollection.AddMvc((x) => { x.EnableEndpointRouting = true; });
            mvcBuilder.SetCompatibilityVersion(CompatibilityVersion.Latest);
            mvcBuilder.AddNewtonsoftJson();

            CommonConfigureServices(serviceCollection);
            SpecificConfigureServices(serviceCollection);
        }

        private void CommonConfigureServices(IServiceCollection serviceCollection) 
        {
            serviceCollection.AddTransient<IHttpContextAccessor, HttpContextAccessor>();
            serviceCollection.AddSingleton<IWebHostEnvironment>(_environment);
        }

        public void Configure(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider)
        {
            EarlyBuild(applicationBuilder, serviceProvider);
            
            //if (_environment.EnvironmentName != "Production")
            //    applicationBuilder.UseDeveloperExceptionPage();

            if (_standardConfiguration.Http.HttpsEnabled)
            {
                applicationBuilder.UseFluffySpoonLetsEncryptChallengeApprovalMiddleware();
                applicationBuilder.UseHsts();
                applicationBuilder.UseHttpsRedirection();
            }

            ConfigureFrontend(applicationBuilder);

            applicationBuilder.UseRouting();
            applicationBuilder.UseEndpoints(endpoints =>
            {
                AddDefaultRouting(endpoints);
                AddCustomRouting(endpoints);
            });

            LateBuild(applicationBuilder, serviceProvider);
        }

        private void AddDefaultRouting(IEndpointRouteBuilder endpoints)
        {
            if (_standardConfiguration.Controller.UseDefaultRoutes)
            {
                endpoints.MapControllerRoute(name: "noControllerGiven", pattern: "/{action=Index}", defaults: new { controller = "Default" });
                endpoints.MapControllerRoute(name: "normalPattern", pattern: "{controller}/{action=Index}");
            }
        }

        private void AddCustomRouting(IEndpointRouteBuilder endpoints)
        {
            if (_standardConfiguration.Controller.AdditionalRoutes != null)
                _standardConfiguration.Controller.AdditionalRoutes.ToList().ForEach(x =>
                    endpoints.MapControllerRoute(name: x.Name, pattern: x.Pattern, defaults: new { x.Defaults.Controller, x.Defaults.Action }));
        }

        private void ConfigureFrontend(IApplicationBuilder applicationBuilder)
        {
            if (_standardConfiguration.Controller.IsFrontend)
            {
                applicationBuilder.UseStaticFiles();
                applicationBuilder.UseSession();
            }
        }

        private void ConfigureServicesControllers(IServiceCollection serviceCollection)
        {
            if (_standardConfiguration.Controller.IsFrontend)
            {
                ConfigureSessionCookies(serviceCollection);
                ConfigureCookiePolicy(serviceCollection);
                ConfigureControllers(serviceCollection);
            }
            else
                serviceCollection.AddControllers();
        }

        private void ConfigureControllers(IServiceCollection serviceCollection)
        {
            if (_environment.EnvironmentName == "Development")
                serviceCollection.AddControllersWithViews().AddRazorRuntimeCompilation();
            else
                serviceCollection.AddControllersWithViews();
        }

        private void ConfigureCookiePolicy(IServiceCollection serviceCollection)
        {
            serviceCollection.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => _standardConfiguration.Controller.DoesAnyCookieRequireConsent;
                options.MinimumSameSitePolicy = SameSiteMode.Strict;
            });
        }

        private void ConfigureSessionCookies(IServiceCollection serviceCollection)
        {
            RNGCryptoServiceProvider cryptoRng = new RNGCryptoServiceProvider();
            byte[] randomBytes = new byte[16];
            cryptoRng.GetBytes(randomBytes);
            string randomizedCookieNamePerRuntime = Convert.ToBase64String(randomBytes).Replace("=", "").Replace('/', '_').Replace('+', '-');

            serviceCollection.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(_standardConfiguration.Controller.SessionIdleTimeoutInMinutes);
                options.Cookie.Name = $".{_standardConfiguration.Http.MainPublicDomain ?? Assembly.GetCallingAssembly().GetName().Name}.{randomizedCookieNamePerRuntime}.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
        }

        private void ConfigureServiceHttps(IServiceCollection serviceCollection)
        {
            if (_standardConfiguration.Http.HttpsEnabled)
            {
                serviceCollection.AddHsts(options =>
                {
                    options.Preload = true;
                    options.IncludeSubDomains = false;
                    options.MaxAge = TimeSpan.FromDays(366);
                });

                string[] allDomains = _standardConfiguration.Http.AlternativePublicDomains ?? new string[0];
                allDomains = allDomains.Prepend(_standardConfiguration.Http.MainPublicDomain).ToArray();

                serviceCollection.AddFluffySpoonLetsEncryptRenewalService(new LetsEncryptOptions()
                {
                    Email = _standardConfiguration.Http.CsrInfo.EmailAddress,
                    UseStaging = false,
                    Domains = allDomains,
                    TimeUntilExpiryBeforeRenewal = TimeSpan.FromDays(30),
                    TimeAfterIssueDateBeforeRenewal = TimeSpan.FromDays(7),
                    CertificateSigningRequest = new Certes.CsrInfo()
                    {
                        CountryName = _standardConfiguration.Http.CsrInfo.CountryName,
                        Locality = _standardConfiguration.Http.CsrInfo.Locality,
                        Organization = _standardConfiguration.Http.CsrInfo.Organization,
                        OrganizationUnit = _standardConfiguration.Http.CsrInfo.OrganizationUnit,
                        State = _standardConfiguration.Http.CsrInfo.State
                    }
                });

                serviceCollection.AddFluffySpoonLetsEncryptFileCertificatePersistence();
                serviceCollection.AddFluffySpoonLetsEncryptMemoryChallengePersistence();
            }
        }

        private void PrintEnvironment()
        {
            Console.WriteLine($"=============================================");
            Console.WriteLine($"     Running in environment: {_environment.EnvironmentName}");
            Console.WriteLine($"=============================================");
        }
    }
}
