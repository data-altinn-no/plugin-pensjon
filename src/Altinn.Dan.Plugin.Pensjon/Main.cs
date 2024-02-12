using Altinn.Dan.Plugin.Pensjon.Config;
using Altinn.Dan.Plugin.Pensjon.Models;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dan.Common;
using Dan.Common.Exceptions;
using Dan.Common.Models;
using Dan.Common.Util;

namespace Altinn.Dan.Plugin.Pensjon
{
    public class Main
    {
        private ILogger _logger;
        private readonly HttpClient _client;
        private readonly ApplicationSettings _settings;

        public Main(IHttpClientFactory httpClientFactory, IOptions<ApplicationSettings> settings)
        {
            _client = httpClientFactory.CreateClient("ECHttpClient");
            _settings = settings.Value;
        }

        [Function("NorskPensjon")]
        public async Task<HttpResponseData> GetNorskPensjon(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            _logger = context.GetLogger(context.FunctionDefinition.Name);
            _logger.LogInformation("Running func 'NorskPensjon'");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var evidenceHarvesterRequest = JsonConvert.DeserializeObject<EvidenceHarvesterRequest>(requestBody);

            return await EvidenceSourceResponse.CreateResponse(req, () => GetEvidenceValuesPensjon(evidenceHarvesterRequest));
        }

        private async Task<List<EvidenceValue>> GetEvidenceValuesPensjon(EvidenceHarvesterRequest evidenceHarvesterRequest)
        {
            var content = await MakeRequest(_settings.NorskPensjonUrl, evidenceHarvesterRequest.SubjectParty);

            var ecb = new EvidenceBuilder(new Metadata(), "NorskPensjon");
            //_logger.LogInformation(content);
            ecb.AddEvidenceValue("default", content, Metadata.SOURCE, false);

            return ecb.GetEvidenceValues();
        }

        private async Task<string> MakeRequest(string target, Party subject) 
        {
            HttpResponseMessage result = null;
            var requestBody = new NorskPensjonRequest
            {
                fodselsnummer = subject.NorwegianSocialSecurityNumber
            };

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, target);
                request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                result = await _client.SendAsync(request);
                switch (result.StatusCode)
                {
                    case HttpStatusCode.OK:
                    {
                        return await result.Content.ReadAsStringAsync();
                    }
                    case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    {
                        _logger.LogError($"Authentication failed for Norsk Pensjon for {subject.GetAsString()} - {result.Content}");

                        throw new EvidenceSourcePermanentClientException(Metadata.ERROR_ORGANIZATION_NOT_FOUND, $"Authentication failed ({(int)result.StatusCode})");
                    }
                    case HttpStatusCode.InternalServerError:
                    {
                        _logger.LogError($"Call to Norsk Pensjon failed (500 - internal server error)");

                        throw new EvidenceSourceTransientException(Metadata.ERROR_CCR_UPSTREAM_ERROR);
                    }
                    default:
                    {
                        _logger.LogError($"Unexpected status code from external API ({(int)result.StatusCode} - {result.StatusCode})");

                        throw new EvidenceSourcePermanentClientException(Metadata.ERROR_CCR_UPSTREAM_ERROR,
                            $"External API call to Norsk Pensjon failed ({(int)result.StatusCode} - {result.StatusCode})");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex.Message);

                throw new EvidenceSourcePermanentServerException(Metadata.ERROR_CCR_UPSTREAM_ERROR, null, ex);
            }
        }

        [Function(Constants.EvidenceSourceMetadataFunctionName)]
        public async Task<HttpResponseData> GetMetadata(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            _logger = context.GetLogger(context.FunctionDefinition.Name);
            _logger.LogInformation($"Running func metadata for {Constants.EvidenceSourceMetadataFunctionName}");
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new Metadata().GetEvidenceCodes(),
                new NewtonsoftJsonObjectSerializer(new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto }));

            return response;
        }
    }
}
