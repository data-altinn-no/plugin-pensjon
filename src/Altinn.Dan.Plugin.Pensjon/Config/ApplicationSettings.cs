using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;

namespace Altinn.Dan.Plugin.Pensjon.Config
{
    public class ApplicationSettings
    {
        private X509Certificate2 _cert;
        public string RedisConnectionString { get; set; }
        public TimeSpan BreakerRetryWaitTime { get; set; }
        public string NorskPensjonUrl { get; set; }
        public string KeyVaultName { get; set; }
        public string CertificateName { get; set; }

        public X509Certificate2 Certificate
        {
            get
            {
                if (_cert == null)
                {
                    var certificateClient = new CertificateClient(new Uri($"https://{KeyVaultName}.vault.azure.net/"), new DefaultAzureCredential());
                    var keyVaultCertificateWithPolicy = certificateClient.GetCertificate(CertificateName).Value;
                    _cert = new X509Certificate2(keyVaultCertificateWithPolicy.Cer);
                }
                return _cert;
            }

            set
            {
                _cert = value;
            }
        }
    }
}
