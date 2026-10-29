using Azure.Identity;
using Biz.Bizadm.KMS.Cipher;
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
        private const string KEY_NAME = "RedmineMCPKey";

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

                    using X509Store x509Store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    x509Store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                    using X509Certificate2 certificate = x509Store.Certificates.Find(X509FindType.FindByThumbprint, configuration.AzureKms!.ClientCertificateThumbprint, false)
                        .FirstOrDefault(x => x.HasPrivateKey) ?? throw new InvalidOperationException("개인 키가 포함된 인증서를 찾을 수 없습니다.");

                    ClientCertificateCredential credential = new ClientCertificateCredential(
                        configuration.AzureKms.AzureTenantId,
                        configuration.AzureKms.AzureClientId,
                        certificate
                    );

                    using AzureKeyVaultKekManager manager = await AzureKeyVaultKekManager.CreateAsync(
                        new Uri(configuration.AzureKms.AzureKeyVaultUrl),
                        credential,
                        KEY_NAME
                    );

                    using IDekCipherService dekCipherService = new DekCipherService(
                        AesGcmDekCipher.Create(manager.Current, new FileInfo(Path.Combine(configuration.ContentRootPath, configuration.AzureKms.SecretsDekFilePath)))
                    );

                    bool configRewritten = false;

                    if (configuration.Secure!.ServerCertificatePassword is not null
                        && !dekCipherService.StartsWithFromPrefix(configuration.Secure.ServerCertificatePassword))
                    {
                        configuration.Secure.ServerCertificatePassword = dekCipherService.Encrypt(configuration.Secure.ServerCertificatePassword);
                        configRewritten = true;
                    }

                    if (!dekCipherService.StartsWithFromPrefix(configuration.Redmine!.RedmineApiKey))
                    {
                        configuration.Redmine.RedmineApiKey = dekCipherService.Encrypt(configuration.Redmine.RedmineApiKey);
                        configRewritten = true;
                    }

                    if (configRewritten)
                    {
                        YamlDotNet.Serialization.Serializer serializer = new YamlDotNet.Serialization.Serializer();
                        File.WriteAllText(cmdMain.ConfigFilePath, serializer.Serialize(configuration));
                    }

                    if (configuration.Secure.ServerCertificatePassword is not null)
                    {
                        configuration.Secure.ServerCertificatePassword = dekCipherService.Decrypt(configuration.Secure.ServerCertificatePassword);
                    }

                    configuration.Redmine.RedmineApiKey = dekCipherService.Decrypt(configuration.Redmine.RedmineApiKey);

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
                if (configuration.Secure!.UseSsl!.Value)
                {
                    options.ListenAnyIP(configuration.Port!.Value, configure =>
                    {
                        X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(
                            new FileInfo(Path.Combine(configuration.ContentRootPath, configuration.Secure.ServerCertificate!)).FullName,
                            configuration.Secure.ServerCertificatePassword
                        );

                        configure.UseHttps(httpsOptions =>
                        {
                            httpsOptions.ServerCertificate = certificate;
                            if (configuration.Secure.CertificateChain is not null)
                            {
                                X509Certificate2Collection collection = X509CertificateLoader.LoadPkcs12CollectionFromFile(
                                    new FileInfo(configuration.Secure.ServerCertificate!).FullName,
                                    configuration.Secure.ServerCertificatePassword
                                );

                                Dictionary<string, X509Certificate2> certificates = new Dictionary<string, X509Certificate2>();
                                foreach (X509Certificate2 certificate in collection.Where(cert => !cert.HasPrivateKey))
                                {
                                    string name = certificate.GetNameInfo(X509NameType.SimpleName, false);
                                    certificates.Add(name, certificate);
                                }

                                string[] certificateChain =
                                    configuration.Secure.CertificateChain.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                                httpsOptions.ServerCertificateChain = new X509Certificate2Collection();

                                foreach (string name in certificateChain)
                                {
                                    if (certificates.TryGetValue(name, out X509Certificate2? certificate))
                                        httpsOptions.ServerCertificateChain.Add(certificate);
                                }
                            }

                            httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                            if (configuration.Secure.IncludeCipherSuites is not null)
                            {
                                string[] includeCipherSuites =
                                configuration.Secure.IncludeCipherSuites.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

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
                .WithTools<AttachmentTools>()
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
