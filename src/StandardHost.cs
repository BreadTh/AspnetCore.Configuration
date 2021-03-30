using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using FluffySpoon.AspNet.LetsEncrypt.Certes;

using BreadTh.AspNet.Configuration.Core;

namespace BreadTh.AspNet.Configuration
{
    public class StandardHost<STARTUP> where STARTUP : StandardStartup
    {
        const SslProtocols sslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        static readonly CipherSuitesPolicy ChosenCipherSuitePolicy =
            new CipherSuitesPolicy(
                new TlsCipherSuite[]
                {
					// Experimental TLS 1.3 cipher suites:
					TlsCipherSuite.TLS_AES_128_GCM_SHA256
                ,   TlsCipherSuite.TLS_AES_256_GCM_SHA384
                ,   TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256

					// Standard TLS 1.2 cipher suites:
				,   TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256
                ,   TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256
                ,   TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384
                ,   TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384
                ,   TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256
                ,   TlsCipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256
                ,   TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256
                ,   TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384
                });

        public IHost Build(string[] args)
        {
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);
            hostBuilder.UseContentRoot(Directory.GetCurrentDirectory());
            ConfigureLogging(hostBuilder);
            hostBuilder.ConfigureWebHostDefaults(webHostBuilder =>
            {
                IConfigurationRoot configuration = MakeConfiguration(args, webHostBuilder);
                webHostBuilder.UseConfiguration(configuration);
                StandardConfiguration standardConfiguration = configuration.GetSection("StandardConfiguration").Get<StandardConfiguration>();

                if (standardConfiguration.Http.HttpsEnabled)
                    ConfigureWebHostForHttps(webHostBuilder, standardConfiguration);
                else 
                {
                    webHostBuilder.UseKestrel();
                    webHostBuilder.UseUrls($"http://0.0.0.0:{standardConfiguration.Http.HttpPort}");
                }

                webHostBuilder.UseStartup<STARTUP>();
            });

            return hostBuilder.Build();
        }

        private static IConfigurationRoot MakeConfiguration(string[] args, IWebHostBuilder webHostBuilder) =>
            new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddJsonFile("./appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"./appsettings.{webHostBuilder.GetSetting("environment")}.json", optional: true, reloadOnChange: true)
                .AddCommandLine(args)
                .Build();
        

        private static void ConfigureWebHostForHttps(IWebHostBuilder webHostBuilder, StandardConfiguration standardConfiguration)
        {
            webHostBuilder.UseUrls($"http://0.0.0.0:{standardConfiguration.Http.HttpPort}", $"https://0.0.0.0:{standardConfiguration.Http.HttpsPort}");
            webHostBuilder.UseKestrel(kestrelOptions =>
                kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificateSelector = SslCertificateSelector;
                    httpsOptions.SslProtocols = sslProtocols;
                    httpsOptions.OnAuthenticate = SslOptionConfigurer;
                }));
        }

        private static X509Certificate2 SslCertificateSelector(ConnectionContext c, string s) =>
            LetsEncryptRenewalService.Certificate;

        private static void SslOptionConfigurer(ConnectionContext conContext, SslServerAuthenticationOptions sslAuthOptions) =>
            sslAuthOptions.CipherSuitesPolicy = ChosenCipherSuitePolicy;

        private static void ConfigureLogging(IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();
                logging.AddEventSourceLogger();
            });
        }
    }
}
