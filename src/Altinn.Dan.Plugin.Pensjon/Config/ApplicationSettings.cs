using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Altinn.Dan.Plugin.Pensjon.Config
{
    public class ApplicationSettings
    {
        private readonly object _certificateHeaderLock = new();
        private X509Certificate2 _cert;
        private string _certificateHeaderValue;
        public string RedisConnectionString { get; set; }
        public TimeSpan BreakerRetryWaitTime { get; set; }
        public string NorskPensjonUrl { get; set; }
        public string KeyVaultName { get; set; }
        public string CertificateName { get; set; }

        //for production we need to use proxy
        public bool UseProxy { get; set; }

        public string ProxyUrl { get; set; }

        public string CustomCertificateHeaderName { get; set; }

        public X509Certificate2 Certificate
        {
            get
            {
                if (_cert == null)
                {
                    var secretClient = new SecretClient(new Uri($"https://{KeyVaultName}.vault.azure.net/"),
                        new DefaultAzureCredential());
                    var certWithPrivateKey = secretClient.GetSecret(CertificateName).Value;
                    _cert = new X509Certificate2(Convert.FromBase64String(certWithPrivateKey.Value), string.Empty, X509KeyStorageFlags.Exportable);
                }
                return _cert;
            }

            set
            {
                lock (_certificateHeaderLock)
                {
                    _cert = value;
                    _certificateHeaderValue = null;
                }
            }
        }

        /// <summary>
        /// The certificate with its private key as it is sent to the proxy: a password-less PFX, base64 encoded.
        ///
        /// Exported once and then reused. A PFX export contains random salt, so exporting for every
        /// request gives a different value each time, and the proxy only reuses its connections to
        /// Norsk Pensjon for requests that carry exactly the same header value.
        /// </summary>
        public string CertificateHeaderValue
        {
            get
            {
                lock (_certificateHeaderLock)
                {
                    return _certificateHeaderValue ??= Convert.ToBase64String(Certificate.Export(X509ContentType.Pkcs12));
                }
            }
        }
    }
}
