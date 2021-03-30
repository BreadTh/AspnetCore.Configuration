using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FluffySpoon.AspNet.LetsEncrypt;
using FluffySpoon.AspNet.LetsEncrypt.Certes;
using BreadTh.StronglyApied.AspNet;

using BreadTh.AspNet.Configuration.Core;

namespace BreadTh.AspNet.Configuration
{
    public abstract class StandardStartup
    {
        protected readonly IWebHostEnvironment _environment;
        protected readonly IConfiguration _configuration;
        readonly StandardConfiguration _standardConfiguration;

        protected StandardStartup(IWebHostEnvironment environment, IConfiguration configuration) 
        {
            _environment = environment;
            _configuration = configuration;
            _standardConfiguration = _configuration.GetSection("StandardConfiguration").Get<StandardConfiguration>();
        }

        protected virtual void SpecificConfigureServices(IServiceCollection serviceCollection) { }
        protected virtual void EarlyBuild(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider) { }
        protected virtual void LateBuild(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider) { }
        protected virtual void BuildBetweenRoutingAndEndpoints(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider) { }
        protected virtual void ControllerOptions(MvcOptions options) { }
        protected virtual async Task<AuthenticateResult> OnAuthenticateAttempt(
            HttpContext httpContext, AuthenticationScheme scheme, IServiceProvider serviceProvider) => 
                await Task.FromResult(AuthenticateResult.Fail("Authentication not configured"));
        protected virtual async Task OnNotAuthenticated(HttpContext context) => await Task.CompletedTask;
        protected virtual async Task OnNotAuthorized(HttpContext context) => await Task.CompletedTask;
        protected virtual async Task OnNotFound(HttpContext context) => await Task.CompletedTask;
        protected abstract Task OnUnhandledException(Exception exception, HttpContext httpContext);
        protected abstract Task OnStronglyApiedInputBodyValidationError(HttpContext httpContext, List<ErrorDescription> errors);
        
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
            serviceCollection.AddSingleton(this);
            
            serviceCollection.AddAuthentication("Standard")
                .AddScheme<StandardAuthenticationOptions, StandardAuthenticationHandler>("Standard", null);
            serviceCollection.AddHttpContextAccessor();
        }

        public void Configure(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider)
        {
            if (_standardConfiguration.UseDeveloperExceptionPage)
                applicationBuilder.UseDeveloperExceptionPage();
            else
                AddExceptionHandler(applicationBuilder);
            
            EarlyBuild(applicationBuilder, serviceProvider);

            applicationBuilder.UseStronglyApiedParseErrorResponse(OnStronglyApiedInputBodyValidationError);

            if (_standardConfiguration.Http.HttpsEnabled)
            {
                applicationBuilder.UseFluffySpoonLetsEncryptChallengeApprovalMiddleware();
                applicationBuilder.UseHsts();
                applicationBuilder.UseHttpsRedirection();
            }

            ConfigureFrontend(applicationBuilder);

            applicationBuilder.UseRouting();

            applicationBuilder.UseAuthentication();
            applicationBuilder.Use(async (context, next) => 
            {
                await next.Invoke();

                if(context.Response.StatusCode == 401)
                    await OnNotAuthenticated(context);
                else if(context.Response.StatusCode == 403)
                    await OnNotAuthorized(context);
                else if (context.Response.StatusCode is >= 404 and <= 405)
                    await OnNotFound(context);
            });

            applicationBuilder.UseAuthorization();

            applicationBuilder.UseEndpoints(endpoints =>
            {
                AddDefaultRouting(endpoints);
                AddCustomRouting(endpoints);
            });

            LateBuild(applicationBuilder, serviceProvider);
        }

        private void AddExceptionHandler(IApplicationBuilder applicationBuilder) =>
            _ = applicationBuilder.Use(async (HttpContext httpContext, Func<Task> next) =>
            {
                httpContext.Request.EnableBuffering(); 

                try
                {
                    await next();
                }
                catch (Exception exception)
                {
                    await OnUnhandledException(exception, httpContext);
                }
            });

        private void AddDefaultRouting(IEndpointRouteBuilder endpoints)
        {
            if (_standardConfiguration.Controller.UseDefaultRoutes)
            {
                endpoints.MapControllerRoute(name: "noControllerGiven", pattern: "/{action=Index}", defaults: new { controller = "Default" });
                endpoints.MapControllerRoute(name: "normalPattern", pattern: "{controller}/{action=Index}");
            }
            else
                endpoints.MapControllers();
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
                serviceCollection.AddControllers(ConfigureMvc);
        }

        private void ConfigureControllers(IServiceCollection serviceCollection)
        {
            if (_environment.EnvironmentName == "Development")
                serviceCollection.AddControllersWithViews(ConfigureMvc)
                    .AddRazorRuntimeCompilation();
            else
                serviceCollection.AddControllersWithViews(ConfigureMvc);
        }

        private void ConfigureCookiePolicy(IServiceCollection serviceCollection)
        {
            serviceCollection.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => _standardConfiguration.Controller.DoesAnyCookieRequireConsent;
                options.MinimumSameSitePolicy = SameSiteMode.Strict;
            });
        }

        private void ConfigureMvc(MvcOptions options)
        {
            options.UseStronglyApiedInputParser();
            ControllerOptions(options);
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

        private class StandardAuthenticationOptions : AuthenticationSchemeOptions { };
        
        private class StandardAuthenticationHandler : AuthenticationHandler<StandardAuthenticationOptions>
        {
            IServiceProvider serviceProvider;
            StandardStartup parent;
            public StandardAuthenticationHandler(
                IOptionsMonitor<StandardAuthenticationOptions> options
            ,   ILoggerFactory logger
            ,   UrlEncoder encoder
            ,   ISystemClock clock
            ,   IServiceProvider serviceProvider
            ,   StandardStartup parent)
                :   base(options, logger, encoder, clock)
            {
                this.serviceProvider = serviceProvider;
                this.parent = parent;
            }
 
            protected override async Task<AuthenticateResult> HandleAuthenticateAsync() => 
                await parent.OnAuthenticateAttempt(Context, Scheme, serviceProvider);
        }
    }
}
