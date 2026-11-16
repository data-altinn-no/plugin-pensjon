using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Dan.Plugin.Pensjon.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Altinn.Dan.Plugin.Pensjon.Test
{
    [TestClass]
    public class ApplicationSettingsTests
    {
        private static X509Certificate2 CreateCertificate(string commonName)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            // Loaded the same way as the certificate from Key Vault: from PFX, with an exportable key.
            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), string.Empty, X509KeyStorageFlags.Exportable);
        }

        [TestMethod]
        public void CertificateHeaderValue_IsStableAfterConfigurationIsBound()
        {
            // Program binds the whole configuration onto the settings, and the binder reads and
            // writes the properties while doing so. The header value must settle on one value afterwards.
            using var certificate = CreateCertificate("pensjon-test");
            var settings = new ApplicationSettings { Certificate = certificate };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["NorskPensjonUrl"] = "https://example.test",
                    ["UseProxy"] = "true"
                })
                .Build();

            configuration.Bind(settings);

            Assert.AreEqual("https://example.test", settings.NorskPensjonUrl);
            Assert.IsTrue(settings.UseProxy);
            Assert.AreEqual(settings.CertificateHeaderValue, settings.CertificateHeaderValue);
        }

        [TestMethod]
        public void ExportingACertificateTwice_GivesDifferentBytes()
        {
            // The reason CertificateHeaderValue exists: a PFX export contains random salt.
            using var certificate = CreateCertificate("pensjon-test");

            var first = Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
            var second = Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void CertificateHeaderValue_IsIdenticalOnEveryRead()
        {
            using var certificate = CreateCertificate("pensjon-test");
            var settings = new ApplicationSettings { Certificate = certificate };

            var first = settings.CertificateHeaderValue;
            var second = settings.CertificateHeaderValue;

            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public async Task CertificateHeaderValue_ConcurrentFirstReads_GiveOneValue()
        {
            using var certificate = CreateCertificate("pensjon-test");
            var settings = new ApplicationSettings { Certificate = certificate };

            var values = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => settings.CertificateHeaderValue)));

            Assert.AreEqual(1, values.Distinct().Count());
        }

        [TestMethod]
        public void CertificateHeaderValue_IsThePfxOfTheCertificateWithItsPrivateKey()
        {
            using var certificate = CreateCertificate("pensjon-test");
            var settings = new ApplicationSettings { Certificate = certificate };

            using var decoded = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(settings.CertificateHeaderValue), password: null);

            Assert.AreEqual(certificate.Thumbprint, decoded.Thumbprint);
            Assert.IsTrue(decoded.HasPrivateKey);
        }

        [TestMethod]
        public void CertificateHeaderValue_FollowsANewCertificate()
        {
            using var firstCertificate = CreateCertificate("pensjon-test-1");
            using var secondCertificate = CreateCertificate("pensjon-test-2");
            var settings = new ApplicationSettings { Certificate = firstCertificate };
            var firstValue = settings.CertificateHeaderValue;

            settings.Certificate = secondCertificate;

            using var decoded = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(settings.CertificateHeaderValue), password: null);
            Assert.AreNotEqual(firstValue, settings.CertificateHeaderValue);
            Assert.AreEqual(secondCertificate.Thumbprint, decoded.Thumbprint);
        }
    }
}
