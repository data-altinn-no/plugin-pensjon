using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Altinn.Dan.Plugin.Pensjon.Config;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Caching.Distributed;
using Polly.Extensions.Http;
using Polly.Registry;

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
                        })                       
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(ApplicationSettings.Certificate);

                            return handler;
                        });

                    services.Configure<JsonSerializerOptions>(options =>
                    {
                        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                        options.Converters.Add(new JsonStringEnumConverter());
                    });
                })
                .Build();

            return host.RunAsync();
        }
    }
}
