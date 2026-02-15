using System.Linq;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;
using ByteBard.AsyncAPI.Bindings.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StreetlightsAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging => logging.AddSimpleConsole(console => console.SingleLine = true))
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseStartup<Startup>();
                });
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAsyncApi(e =>
            {
                e.AddServer("mosquitto", "test.mosquitto.org", "mqtt", server =>
                {
                    server.Description = "Test Mosquitto MQTT Broker";
                });
                e.AddServer("webapi", "localhost:5000", "http", server =>
                {
                    server.Description = "Local HTTP API Server";
                });
                e.WithDefaultContentType("application/json")
                    .WithDescription(
                        "The Smartylighting Streetlights API allows you to remotely manage the city lights.")
                    .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");
                e.AddChannelBinding("amqpDev",
                        new ByteBard.AsyncAPI.Bindings.AMQP.AMQPChannelBinding()
                        {
                            Is = ByteBard.AsyncAPI.Bindings.AMQP.ChannelType.Queue,
                            Queue = new ByteBard.AsyncAPI.Bindings.AMQP.Queue() { Name = "example-exchange", Vhost = "/development" }
                        }).AddChannelBinding("amqpDev2",
                        new ByteBard.AsyncAPI.Bindings.AMQP.AMQPChannelBinding()
                        {
                            Is = ByteBard.AsyncAPI.Bindings.AMQP.ChannelType.RoutingKey,
                            Exchange = new ByteBard.AsyncAPI.Bindings.AMQP.Exchange() { Name = "example-exchange", Vhost = "/development" }
                        })
                    .AddOperationBinding("postBind",
                        new HttpOperationBinding()
                        {
                            Method = "POST"
                        });
            });


            services.AddScoped<IStreetlightMessageBus, StreetlightMessageBus>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseDeveloperExceptionPage();

            app.UseRouting();
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod());

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAsyncApi();
                endpoints.MapAsyncApiUi();
                endpoints.MapControllers();
            });

            // Print the AsyncAPI doc location
            var logger = app.ApplicationServices.GetService<ILoggerFactory>().CreateLogger<Program>();
            var addresses = app.ServerFeatures.Get<IServerAddressesFeature>().Addresses;

            logger.LogInformation("AsyncAPI doc available at: {URL}",
                $"{addresses.FirstOrDefault()}/asyncapi/asyncapi.json");
            logger.LogInformation("AsyncAPI UI available at: {URL}", $"{addresses.FirstOrDefault()}/asyncapi/ui/");
        }
    }
}
