using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Dan.Plugin.Pensjon.Config;
using Dan.Common.Exceptions;
using Dan.Common.Models;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Altinn.Dan.Plugin.Pensjon.Test
{
    [TestClass]
    public class MainTests
    {
        private const string NorskPensjonUrl = "https://norskpensjon.test/api";
        private const string ProxyKeyHeader = "x-functions-key";

        // Not a real national identity number.
        private static readonly Party Subject = new() { NorwegianSocialSecurityNumber = "00000000000" };

        /// <summary>The plugin with a client that gets this status code back on every call.</summary>
        private static Main CreateMain(HttpStatusCode answer)
        {
            var settings = new ApplicationSettings { NorskPensjonUrl = NorskPensjonUrl, UseProxy = false };

            return new Main(new ClientFactory(new AnswersWith(answer)), Options.Create(settings));
        }

        [TestMethod]
        [DataRow(HttpStatusCode.BadGateway)]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        [DataRow(HttpStatusCode.GatewayTimeout)]
        public async Task AnswerThatSaysNorskPensjonWasNotReached_IsATransientError(HttpStatusCode answer)
        {
            // The proxy answers 502 when it cannot reach Norsk Pensjon and 504 when Norsk Pensjon
            // does not answer in time. 503 is what the proxy app answers while it restarts.
            // The same call can succeed a moment later.
            var main = CreateMain(answer);

            await Assert.ThrowsExactlyAsync<EvidenceSourceTransientException>(() => main.MakeRequest(NorskPensjonUrl, Subject));
        }

        [TestMethod]
        public async Task InternalServerError_IsATransientError()
        {
            var main = CreateMain(HttpStatusCode.InternalServerError);

            await Assert.ThrowsExactlyAsync<EvidenceSourceTransientException>(() => main.MakeRequest(NorskPensjonUrl, Subject));
        }

        [TestMethod]
        public async Task OtherUnexpectedStatusCode_IsAPermanentError()
        {
            var main = CreateMain(HttpStatusCode.NotFound);

            await Assert.ThrowsExactlyAsync<EvidenceSourcePermanentClientException>(() => main.MakeRequest(NorskPensjonUrl, Subject));
        }

        // The key of the proxy

        [TestMethod]
        public async Task ProxyKey_IsSentToTheProxyInAHeader()
        {
            using var certificate = CreateCertificate();
            var proxy = new AnswersWith(HttpStatusCode.OK, "{\"poliser\":[]}");
            var settings = new ApplicationSettings
            {
                NorskPensjonUrl = NorskPensjonUrl,
                UseProxy = true,
                ProxyUrl = "https://proxy.test/DanProxy?url={0}",
                ProxyKey = "key-of-the-proxy",
                Certificate = certificate
            };

            await new Main(new ClientFactory(proxy), Options.Create(settings)).MakeRequest(NorskPensjonUrl, Subject);

            var request = proxy.Requests.Single();
            Assert.IsTrue(request.Headers.TryGetValues(ProxyKeyHeader, out var keys), "The key is sent in a header.");
            Assert.AreEqual("key-of-the-proxy", keys.Single());
            Assert.AreEqual("https://proxy.test/DanProxy?url=norskpensjon.test%2Fapi", request.RequestUri!.AbsoluteUri);
        }

        [TestMethod]
        public async Task WithoutProxyKey_TheAddressInProxyUrlIsCalledAsItIs()
        {
            // An environment that still has the key in ProxyUrl keeps working.
            using var certificate = CreateCertificate();
            var proxy = new AnswersWith(HttpStatusCode.OK, "{\"poliser\":[]}");
            var settings = new ApplicationSettings
            {
                NorskPensjonUrl = NorskPensjonUrl,
                UseProxy = true,
                ProxyUrl = "https://proxy.test/DanProxy?code=key-in-the-address&url={0}",
                Certificate = certificate
            };

            await new Main(new ClientFactory(proxy), Options.Create(settings)).MakeRequest(NorskPensjonUrl, Subject);

            var request = proxy.Requests.Single();
            Assert.IsFalse(request.Headers.Contains(ProxyKeyHeader));
            Assert.AreEqual("https://proxy.test/DanProxy?code=key-in-the-address&url=norskpensjon.test%2Fapi", request.RequestUri!.AbsoluteUri);
        }

        private static X509Certificate2 CreateCertificate()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=pensjon-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        }

        /// <summary>Answers every call with the same status code and body, and keeps the requests it got.</summary>
        private sealed class AnswersWith(HttpStatusCode answer, string body = "") : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(new HttpResponseMessage(answer) { Content = new StringContent(body) });
            }
        }

        private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
        }
    }
}
