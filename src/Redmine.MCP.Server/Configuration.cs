using System.Configuration.Annotation;

namespace Redmine.MCP.Server
{
    public class Configuration : IValidatableConfiguration
    {
        [Property(PropertyType.STRING, required: true)]
        public string ContentRootPath { get; set; } = null!;

        [Property(PropertyType.USHORT, required: true)]
        public ushort? Port { get; set; } = null!;

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

        [Property(PropertyType.STRING, required: true)]
        public string RedmineApiEndpoint { get; set; } = null!;

        [Property(PropertyType.STRING, required: true)]
        public string RedmineApiKey { get; set; } = null!;

        public void Validate()
        {
            if (UseSsl.HasValue && UseSsl.Value)
            {
                ArgumentNullException.ThrowIfNull(ServerCertificate, nameof(ServerCertificate));
                ArgumentNullException.ThrowIfNull(ServerCertificatePassword, nameof(ServerCertificatePassword));
            }
        }
    }
}
