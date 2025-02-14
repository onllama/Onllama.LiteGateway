using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using ProxyKit;

namespace Onllama.LiteGateway
{
    internal class Program
    {
        public static bool UseToken = false;
        public static List<string> TokensList = [];
        public static string TargetApiUrl = "http://127.0.0.1:11434";
        public static List<string> ApiPathList =
            ["/api", "/v1"];

        static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddProxy(httpClientBuilder =>
                        httpClientBuilder.ConfigureHttpClient(client =>
                            client.Timeout = TimeSpan.FromMinutes(5)));
                })
                .ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(18080,
                        listenOptions => listenOptions.Protocols = HttpProtocols.Http1AndHttp2);
                })
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        var reqToken = context.Request.Headers.ContainsKey("Authorization")
                            ? context.Request.Headers.Authorization.ToString().Split(' ').Last().ToString()
                            : string.Empty;

                        if (UseToken && !TokensList.Contains(reqToken))
                        {
                            context.Response.Headers.ContentType = "application/json";
                            context.Response.StatusCode = (int) HttpStatusCode.Forbidden;
                            await context.Response.WriteAsync(new JObject()
                            {
                                {
                                    "error", new JObject
                                    {
                                        {
                                            "message",
                                            "Authentication Fails, You need to provide a valid API key in the Authorization header using Bearer authentication (i.e. Authorization: Bearer YOUR_KEY)."
                                        },
                                        {"type", "invalid_request_error"}
                                    }
                                }
                            }.ToString());
                        }
                        else
                        {
                            context.Items["Token"] = reqToken;
                            await next.Invoke();
                        }
                    });

                    foreach (var path in ApiPathList)
                        app.Map(path, svr =>
                        {
                            svr.RunProxy(async context =>
                            {
                                var response = new HttpResponseMessage();
                                try
                                {
                                    Console.WriteLine(context.Request.PathBase+ context.Request.Path);
                                    response = await context
                                        .ForwardTo(new Uri(TargetApiUrl + context.Request.PathBase)).Send();
                                    response.Headers.Add("X-Forwarder-By", "MondrianGateway/Lite");
                                    return response;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                    return response;
                                }
                            });
                        });

                }).Build();

            host.Run();
        }
    }
}
