using CommandLine;
using Serilog;
using Serilog.Configuration;
using System.Configuration;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Redmine.MCP.Server
{
    internal static class Program
    {
        public sealed class CmdMain
        {
            [Option("config", Required = true, HelpText = "config file path")]
            public string ConfigFilePath { get; set; } = null!;

            [Option("log", Required = true, HelpText = "log dir path")]
            public string LogDirPath { get; set; } = null!;
        }

        private static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<CmdMain>(args)
                .WithParsedAsync(async cmdMain =>
                {
                    YamlDotNet.Serialization.Deserializer deserializer = new YamlDotNet.Serialization.Deserializer();
                    Configuration? configuration = deserializer.Deserialize<Configuration>(File.ReadAllText(cmdMain.ConfigFilePath));
                    ConfigurationValidator.Validate(configuration);
                    WebApplication app = CreateWebApplication(args, cmdMain, configuration);
                    await RunAsync(app);
                });
        }

        private static WebApplication CreateWebApplication(string[] args, CmdMain cmdMain, Configuration configuration)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = configuration.ContentRootPath
            });

            builder.Host.UseWindowsService();

            builder.WebHost.UseKestrel(options =>
            {
                if (configuration.UseSsl!.Value)
                {
                    options.ListenAnyIP(configuration.Port!.Value, configure =>
                    {
                        X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(
                            new FileInfo(Path.Combine(configuration.ContentRootPath, configuration.ServerCertificate!)).FullName,
                            configuration.ServerCertificatePassword
                        );

                        configure.UseHttps(httpsOptions =>
                        {
                            httpsOptions.ServerCertificate = certificate;
                            if (configuration.CertificateChain is not null)
                            {
                                X509Certificate2Collection collection = X509CertificateLoader.LoadPkcs12CollectionFromFile(
                                    new FileInfo(configuration.ServerCertificate!).FullName,
                                    configuration.ServerCertificatePassword
                                );

                                Dictionary<string, X509Certificate2> certificates = new Dictionary<string, X509Certificate2>();
                                foreach (X509Certificate2 certificate in collection.Where(cert => !cert.HasPrivateKey))
                                {
                                    string name = certificate.GetNameInfo(X509NameType.SimpleName, false);
                                    certificates.Add(name, certificate);
                                }

                                string[] certificateChain =
                                    configuration.CertificateChain.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                                httpsOptions.ServerCertificateChain = new X509Certificate2Collection();

                                foreach (string name in certificateChain)
                                {
                                    if (certificates.TryGetValue(name, out X509Certificate2? certificate))
                                        httpsOptions.ServerCertificateChain.Add(certificate);
                                }
                            }

                            httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                            if (configuration.IncludeCipherSuites is not null)
                            {
                                string[] includeCipherSuites =
                                configuration.IncludeCipherSuites.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                                if (includeCipherSuites.Length > 0)
                                {
                                    httpsOptions.OnAuthenticate = (connectionContext, authenticationOptions) =>
                                    {
                                        authenticationOptions.AllowRenegotiation = false;
                                        HashSet<TlsCipherSuite> tlsCipherSuites = new HashSet<TlsCipherSuite>();
                                        foreach (string cipherSuite in includeCipherSuites)
                                        {
                                            if (Enum.TryParse(cipherSuite, out TlsCipherSuite tlsCipherSuite))
                                                tlsCipherSuites.Add(tlsCipherSuite);
                                        }


                                        if (tlsCipherSuites.Count > 0)
                                            authenticationOptions.CipherSuitesPolicy = new CipherSuitesPolicy(tlsCipherSuites);
                                    };
                                }
                            }
                        });
                    });
                }
                else
                    options.ListenAnyIP(configuration.Port!.Value);

                options.ConfigureEndpointDefaults(configureOptions =>
                {
                    configureOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3;
                });
            });

            builder.Services.AddSerilog(configureLogger =>
            {
                configureLogger
                    .MinimumLevel.Information()
                    .Enrich.WithCaller()
                    .WriteTo.Console(
                        Serilog.Events.LogEventLevel.Information,
                        CallerEnricherOutputTemplate.Default
                    )
                    .WriteTo.File(
                            new DirectoryInfo(cmdMain.LogDirPath).FullName,
                            "Redmine.MCP.Server.log",
                            Serilog.Events.LogEventLevel.Information,
                            CallerEnricherOutputTemplate.Default,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 12
                    );
            });

            builder.Services.AddSingleton(configuration);
            builder.Services.AddHttpClient<RedmineClient>((_, client) =>
            {
                RedmineHttpClientSetup.Configure(client, configuration);
            });

            // Add the MCP services: the transport to use (http) and the tools to register.
            builder.Services
                .AddMcpServer()
                .WithHttpTransport(options =>
                {
                    // Stateless mode is recommended for servers that don't need
                    // server-to-client requests like sampling or elicitation.
                    // See https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html for details.
                    options.Stateless = true;
                })
                .WithTools<IssueTools>()
                .WithTools<ProjectTools>()
                .WithTools<UserTools>()
                .WithTools<TimeEntryTools>()
                .WithTools<WikiTools>()
                .WithTools<MetadataTools>()
                .WithTools<TextGuideTools>();

            return builder.Build();
        }

        private static async Task RunAsync(WebApplication app)
        {
            app.MapMcp();

            await app.RunAsync();
        }
    }
}