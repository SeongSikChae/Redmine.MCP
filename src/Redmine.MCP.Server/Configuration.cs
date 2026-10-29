using System.Configuration;
using System.Configuration.Annotation;

namespace Redmine.MCP.Server
{
    public class Configuration : IValidatableConfiguration
    {
        [Property(PropertyType.STRING, required: true)]
        public string ContentRootPath { get; set; } = null!;

        [Property(PropertyType.USHORT, required: true)]
        public ushort? Port { get; set; } = null!;

        public SecureConfiguration? Secure { get; set; }

        public AzureKmsConfiguration? AzureKms { get; set; }

        public RedmineConfiguration? Redmine { get; set; }

        public void Validate()
        {
            ConfigurationValidator.Validate(Secure);
            ConfigurationValidator.Validate(AzureKms);
            ConfigurationValidator.Validate(Redmine);
        }
    }

    public sealed class SecureConfiguration : IValidatableConfiguration
    {
        [Property(PropertyType.BOOL, required: true)]
        public bool? UseSsl { get; set; } = null!;

        [Property(PropertyType.STRING)]
        public string? ServerCertificate { get; set; }

        [Property(PropertyType.STRING)]
        public string? ServerCertificatePassword { get; set; }

        [Property(PropertyType.STRING, required: false)]
        public string? CertificateChain { get; set; }

        [Property(PropertyType.STRING, required: false)]
        public string? IncludeCipherSuites { get; set; }

        public void Validate()
        {
            if (UseSsl.HasValue && UseSsl.Value)
            {
                ArgumentNullException.ThrowIfNull(ServerCertificate, nameof(ServerCertificate));
                ArgumentNullException.ThrowIfNull(ServerCertificatePassword, nameof(ServerCertificatePassword));
            }
        }
    }

    public sealed class AzureKmsConfiguration
    {
        [Property(PropertyType.STRING, required: true)]
        public string ClientCertificateThumbprint { get; set; } = null!;

        [Property(PropertyType.STRING, required: true)]
        public string AzureTenantId { get; set; } = null!;

        [Property(PropertyType.STRING, required: true)]
        public string AzureClientId { get; set; } = null!;

        [Property(PropertyType.STRING, required: true)]
        public string AzureKeyVaultUrl { get; set; } = null!;

        [Property(PropertyType.STRING, required: true)]
        public string SecretsDekFilePath { get; set; } = null!;
    }

    public sealed class RedmineConfiguration
    {
        [Property(PropertyType.STRING, required: true)]
        public string RedmineApiEndpoint { get; set; } = null!;

        [Property(PropertyType.STRING, required: true)]
        public string RedmineApiKey { get; set; } = null!;
    }
}
