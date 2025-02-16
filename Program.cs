using System.Net;
using System.Security.Cryptography.X509Certificates;
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
        public static string Hostname = string.Empty;
        public static bool UseToken = true;
        public static bool UsePublicPath = true;
        public static bool DisableModelManagePath = true;
        public static List<string> TokensList = [];
        public static string TargetUrl = "http://127.0.0.1:11434";
        public static string ListenUrl = "http://127.0.0.1:22434";

        public static List<string> ApiPathList = 
            ["/api", "/v1"];
        public static List<string> PublicPathList =
            ["/api/tags", "/api/ps", "/api/show", "/v1/models"];
        public static List<string> ModelManagePathList =
            ["/api/pull", "/api/create", "/api/push", "/api/delete", "/api/copy"];

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
            var keysArgument = cmd.Argument("keys",
                isZh ? "设置要求传入的 API 密钥。" : "Setting up the API key", multipleValues: true);

            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>",
                isZh ? "监听的地址与端口。" : "Set server listening address and port",
                CommandOptionType.SingleValue);
            var targetOption = cmd.Option<string>("-t|--target <Uri>",
                isZh ? "目标地址与端口。" : "Set target address and port",
                CommandOptionType.SingleValue);

            var httpsOption = cmd.Option("-s|--https",
                isZh ? "启用 HTTPS。（默认自签名，不推荐）" : "Set enable HTTPS (Self-signed by default, not recommended)",
                CommandOptionType.NoValue);
            var pemOption = cmd.Option<string>("-pem|--pemfile <FilePath>",
                isZh ? "PEM 证书路径。 <./cert.pem>" : "Set your pem certificate file path <./cert.pem>",
                CommandOptionType.SingleOrNoValue);
            var keyOption = cmd.Option<string>("-key|--keyfile <FilePath>",
                isZh ? "PEM 证书密钥路径。 <./cert.key>" : "Set your pem certificate key file path <./cert.key>",
                CommandOptionType.SingleOrNoValue);
            var hostOption = cmd.Option<string>("-h|--host <Hostname>",
                isZh ? "设置允许的主机名。（默认全部允许）" : "Set the allowed host names. (Allow all by default)",
                CommandOptionType.SingleOrNoValue);

            var noTokenOption = cmd.Option("--no-token",
                isZh ? "禁用 API 密钥验证。" : "Disable API key verification",
                CommandOptionType.NoValue);
            var noDisableModelManageOption = cmd.Option("--no-disable-model-manage",
                isZh ? "允许通过 API 进行模型管理。" : "Enable model management via API",
                CommandOptionType.NoValue);
            var usePublicPath = cmd.Option("--use-model-info-public-path",
                isZh ? "允许无需 APIKEY 通过 API 查看模型信息。" : "Allows get model information via API without APIKEY",
                CommandOptionType.NoValue);

            cmd.OnExecute(() =>
            {
                if (noTokenOption.HasValue()) UseToken = false;
                if (usePublicPath.HasValue()) UsePublicPath = true;
                if (noDisableModelManageOption.HasValue()) DisableModelManagePath = false;
                if (ipOption.HasValue()) ListenUrl = ipOption.Value();
                if (targetOption.HasValue()) TargetUrl = targetOption.Value();
                if (hostOption.HasValue()) Hostname = hostOption.Value();
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
                            listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                                if (httpsOption.HasValue()) listenOptions.UseHttps();
                                if (pemOption.HasValue() && keyOption.HasValue())
                                    listenOptions.UseHttps(X509Certificate2.CreateFromPem(
                                        File.ReadAllText(pemOption.Value()), File.ReadAllText(keyOption.Value())));
                            });
                    })
                    .Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            if (!string.IsNullOrWhiteSpace(Hostname) &&
                                !string.Equals(context.Request.Host.Host, Hostname,
                                    StringComparison.CurrentCultureIgnoreCase))
                            {
                                context.Response.StatusCode = 404;
                                context.Response.ContentType = "text/plain";
                                await context.Response.WriteAsync("Host Not Found");
                                return;
                            }

                            Console.WriteLine(context.Request.Path.ToString().ToLower());

                            if (DisableModelManagePath && ModelManagePathList.Contains(context.Request.Path.ToString().ToLower().Trim()))
                            {
                                context.Response.Headers.ContentType = "application/json";
                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                await context.Response.WriteAsync(new JObject()
                                {
                                    {
                                        "error", new JObject
                                        {
                                            {
                                                "message",
                                                "The model management API has been disabled, please use Ollama CLI."
                                            },
                                            {"type", "invalid_request_error"}
                                        }
                                    }
                                }.ToString());
                                return;
                            }

                            if (UsePublicPath && PublicPathList.Contains(context.Request.Path.ToString().ToLower().Trim()))
                            {
                                await next.Invoke();
                                return;
                            }

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
