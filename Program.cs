using System.Net;
using McMaster.Extensions.CommandLineUtils;
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
        public static bool UseToken = true;
        public static List<string> TokensList = [];
        public static string TargetUrl = "http://127.0.0.1:11434";
        public static string ListenUrl = "http://127.0.0.1:22434";
        public static List<string> ApiPathList = ["/api", "/v1"];

        static void Main(string[] args)
        {
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            var cmd = new CommandLineApplication
            {
                Name = "Onllama.LiteGateway",
                Description = "Onllama.LiteGateway - The simplest Ollama authentication way." +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MIT License"
            };
            cmd.HelpOption("-?|-h|--help|-he");
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>",
                isZh ? "监听的地址与端口。" : "Set server listening address and port",
                CommandOptionType.SingleValue);
            var targetOption = cmd.Option<string>("-t|--target <Uri>",
                isZh ? "目标地址与端口。" : "Set target address and port",
                CommandOptionType.SingleValue);
            var keysArgument = cmd.Argument("keys",
                isZh ? "设置要求传入的 API 密钥。" : "Setting up the API key", multipleValues: true);

            cmd.OnExecute(() =>
            {
                if (ipOption.HasValue()) ListenUrl = ipOption.Value();
                if (targetOption.HasValue()) TargetUrl = targetOption.Value();
                if (keysArgument.Values.Count > 0)
                {
                    UseToken = true;
                    TokensList = keysArgument.Values.ToList();
                }
                else
                {
                    cmd.ShowHelp();
                    return;
                }

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
                        var uri = new Uri(ListenUrl);
                        options.Listen(new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port == 0 ? 11435 : uri.Port),
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
                                        Console.WriteLine(context.Request.PathBase + context.Request.Path);
                                        response = await context
                                            .ForwardTo(new Uri(TargetUrl + context.Request.PathBase)).Send();
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
            });

            cmd.Execute(args);
        }
    }
}
