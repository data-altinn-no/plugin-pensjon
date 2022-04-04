using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

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
                    var secretClient = new SecretClient(new Uri($"https://{KeyVaultName}.vault.azure.net/"), new DefaultAzureCredential());
                    var keyVaultSecret = secretClient.GetSecretAsync(CertificateName).Result.Value;
                    _cert = new X509Certificate2(Convert.FromBase64String(keyVaultSecret.Value));
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
