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

        /// <summary>
        /// The function key of the proxy. When set, it is sent in a header, and ProxyUrl has no
        /// "code" parameter. A key in the address ends up wherever the address is logged.
        /// </summary>
        public string ProxyKey { get; set; }

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
                    LoadCertificate(certWithPrivateKey.Value);
                }
                return _cert;
            }

            set
            {
                lock (_certificateHeaderLock)
                {
                    // The configuration binder can write back the certificate that is already
                    // there. The header value from Key Vault must survive that.
                    if (ReferenceEquals(_cert, value))
                    {
                        return;
                    }

                    _cert = value;
                    _certificateHeaderValue = null;
                }
            }
        }

        /// <summary>
        /// Takes the certificate from the secret as Key Vault holds it: a password-less PFX,
        /// base64 encoded. The secret is also what is sent to the proxy, unchanged.
        ///
        /// The secret may also hold the certificates of the authorities that issued the
        /// certificate. They are sent along as they are. Norsk Pensjon does not need them.
        /// </summary>
        public void LoadCertificate(string base64Pkcs12)
        {
            var certificate = new X509Certificate2(Convert.FromBase64String(base64Pkcs12), string.Empty, X509KeyStorageFlags.Exportable);

            lock (_certificateHeaderLock)
            {
                _cert = certificate;
                _certificateHeaderValue = base64Pkcs12;
            }
        }

        /// <summary>
        /// The certificate with its private key as it is sent to the proxy: a password-less PFX, base64 encoded.
        ///
        /// This is the secret from Key Vault as it is. Every instance of the plugin then sends
        /// the same value, and the proxy only reuses its connections to Norsk Pensjon for
        /// requests that carry exactly the same header value.
        ///
        /// A certificate that was not loaded from Key Vault is exported once and then reused. A
        /// PFX export contains random salt, so exporting for every request would give a
        /// different value each time.
        /// </summary>
        public string CertificateHeaderValue
        {
            get
            {
                // Loads the certificate from Key Vault if that has not been done, which also sets the value.
                var certificate = Certificate;

                lock (_certificateHeaderLock)
                {
                    return _certificateHeaderValue ??= Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
                }
            }
        }
    }
}
