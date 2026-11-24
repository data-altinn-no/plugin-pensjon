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

        /// <summary>
        /// A certificate issued the way an enterprise certificate is: by an intermediate
        /// certificate authority under a root. Returned as the secret value Key Vault holds
        /// when the issuer certificates are stored with it: a base64 encoded PFX.
        /// </summary>
        private static (string SecretWithIssuers, string Thumbprint) CreateIssuedCertificate()
        {
            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            var notAfter = DateTimeOffset.UtcNow.AddDays(1);

            using var rootKey = RSA.Create(2048);
            using var rootWithKey = CreateAuthorityRequest("Test Root", rootKey).CreateSelfSigned(notBefore, notAfter);
            using var root = X509CertificateLoader.LoadCertificate(rootWithKey.RawData);

            using var intermediateKey = RSA.Create(2048);
            var intermediateRequest = CreateAuthorityRequest("Test Intermediate", intermediateKey);
            using var intermediate = intermediateRequest.Create(rootWithKey, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));
            using var intermediateWithKey = intermediate.CopyWithPrivateKey(intermediateKey);

            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=pensjon-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var withoutKey = request.Create(intermediateWithKey, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));
            using var certificate = withoutKey.CopyWithPrivateKey(key);

            // The issuers come first, so the tests also show that the order in the file does not matter.
            var withIssuers = new X509Certificate2Collection { root, intermediate, certificate };

            return (Convert.ToBase64String(withIssuers.Export(X509ContentType.Pkcs12)!), certificate.Thumbprint);
        }

        private static CertificateRequest CreateAuthorityRequest(string commonName, RSA key)
        {
            var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
                certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));

            return request;
        }

        [TestMethod]
        public void CertificateHeaderValue_IsTheValueFromKeyVaultUnchanged()
        {
            // Every instance of the plugin then sends the same value, and the proxy reuses its
            // connections across them.
            var issued = CreateIssuedCertificate();
            var settings = new ApplicationSettings();
            settings.LoadCertificate(issued.SecretWithIssuers);
            using var loaded = settings.Certificate;

            // Compared without Assert.AreEqual, which would write the PFX to the test log on failure.
            Assert.IsTrue(settings.CertificateHeaderValue == issued.SecretWithIssuers);
        }

        [TestMethod]
        public void Certificate_IsTheOneWithThePrivateKeyWhereverItStandsInTheFile()
        {
            var issued = CreateIssuedCertificate();
            var settings = new ApplicationSettings();
            settings.LoadCertificate(issued.SecretWithIssuers);
            using var loaded = settings.Certificate;

            Assert.AreEqual(issued.Thumbprint, loaded.Thumbprint);
            Assert.IsTrue(loaded.HasPrivateKey);
        }

        [TestMethod]
        public void CertificateHeaderValue_FromKeyVault_IsUnchangedAfterConfigurationIsBound()
        {
            // The binder reads and writes the properties. Writing back the certificate that is
            // already there must not replace the value from Key Vault with an export of the certificate alone.
            var issued = CreateIssuedCertificate();
            var settings = new ApplicationSettings();
            settings.LoadCertificate(issued.SecretWithIssuers);
            using var loaded = settings.Certificate;
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["UseProxy"] = "true" })
                .Build();

            configuration.Bind(settings);
            settings.Certificate = loaded;

            Assert.IsTrue(settings.CertificateHeaderValue == issued.SecretWithIssuers);
        }

        [TestMethod]
        public void CertificateHeaderValue_WithIssuerCertificates_IsStillReadAsTheCertificateByTheFirstProxy()
        {
            // The first proxy reads the header with the X509Certificate2 constructor, which
            // takes one certificate from the file. It must get the one with the private key.
            var issued = CreateIssuedCertificate();
            var settings = new ApplicationSettings();
            settings.LoadCertificate(issued.SecretWithIssuers);
            using var loaded = settings.Certificate;

#pragma warning disable SYSLIB0057 // The constructor is what the first proxy uses.
            using var readByFirstProxy = new X509Certificate2(Convert.FromBase64String(settings.CertificateHeaderValue));
#pragma warning restore SYSLIB0057

            Assert.AreEqual(issued.Thumbprint, readByFirstProxy.Thumbprint);
            Assert.IsTrue(readByFirstProxy.HasPrivateKey);
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
