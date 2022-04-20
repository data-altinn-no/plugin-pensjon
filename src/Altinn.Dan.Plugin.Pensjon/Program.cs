using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Dan.Plugin.Pensjon.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Altinn.Dan.Plugin.Pensjon
{
    class Program
    {
        private static ApplicationSettings ApplicationSettings { get; set; }

        private static Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(services =>
                {
                    services.AddLogging();
                    // See https://docs.microsoft.com/en-us/azure/azure-monitor/app/worker-service#using-application-insights-sdk-for-worker-services
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.AddHttpClient();

                    services.AddOptions<ApplicationSettings>()
                        .Configure<IConfiguration>((settings, configuration) => configuration.Bind(settings));
                    ApplicationSettings = services.BuildServiceProvider().GetRequiredService<IOptions<ApplicationSettings>>().Value;

                    services.AddHttpClient("ECHttpClient", client =>
                        {
                            client.DefaultRequestHeaders.Add("Accept", "application/json");
                            client.DefaultRequestHeaders.Add("Content-Type", "application/json");
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(ApplicationSettings.Certificate);

                            return handler;
                        });

                    JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                    {
                        DefaultValueHandling = DefaultValueHandling.Ignore,
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        Converters = new List<JsonConverter>() { new StringEnumConverter() }
                    };
                })
                .Build();

            return host.RunAsync();
        }
    }
}
