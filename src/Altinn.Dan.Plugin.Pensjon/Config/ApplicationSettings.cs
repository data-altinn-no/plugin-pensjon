using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using System;
using System.Security.Cryptography.X509Certificates;

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
                    var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback));
                    var secretBundle = keyVaultClient.GetSecretAsync(KeyVaultName, CertificateName).Result;
                    _cert = new X509Certificate2(Convert.FromBase64String(secretBundle.Value));
                    return _cert;
                } else
                    return _cert;
            }

            set
            {
                _cert = value;
            }
        }
    }
}
