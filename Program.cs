using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AspNetCoreRateLimit;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using ProxyKit;

namespace Onllama.LiteGateway
{
    internal class Program
    {
        public static string Hostname = string.Empty;
        public static bool UseToken = true;
        public static bool UseThinkTrim = true;
        public static bool UsePublicPath = false;
        public static bool UseCorsAny = false;
        public static bool UseLog = false;
        public static bool UseRateLimiting = false;
        public static bool NoModelManagePath = true;
        public static int NumCtx = -1;
        public static List<string> TokensList = [];
        public static string TargetUrl = "http://127.0.0.1:11434";
        public static string ListenUrl = "http://127.0.0.1:22434";

        public static bool UseInputSecurity = false;
        public static string RiskModel = "shieldgemma:2b";
        public static string RiskModelPrompt = string.Empty;
        public static List<string> RiskKeywordsList = ["YES", "UNSAFE"];

        public static List<string> ApiPathList = 
            ["/api", "/v1"];
        public static List<string> PublicPathList =
            ["/api/tags", "/api/ps", "/api/show", "/v1/models"];
        public static List<string> ModelManagePathList =
            ["/api/pull", "/api/create", "/api/push", "/api/delete", "/api/copy"];

        static void Main(string[] args)
        {
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            try
            {
                if ((Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "").Contains("0.0.0.0"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(isZh
                        ? "!!! Ollama 仍然暴露在公网，请移除 OLLAMA_HOST 环境变量。 !!!"
                        : "!!! Ollama is still listening on Any, please remove the OLLAMA_HOST environment variable. !!!");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                if (File.ReadAllText("/etc/systemd/system/ollama.service").Replace(" ", "")
                    .Contains("OLLAMA_HOST=0.0.0.0"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(isZh
                        ? "!!! Ollama 仍然暴露在公网，请移除 /etc/systemd/system/ollama.service 中的 OLLAMA_HOST 环境变量。 !!!"
                        : "!!! Ollama is still listening on Any. Please remove the OLLAMA_HOST environment variable in /etc/systemd/system/ollama.service. !!!");
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }
            catch (Exception)
            {
                // ignored
            }

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

            var logOption = cmd.Option("--log",
                isZh ? "启用详细日志记录。" : "Enable logging",
                CommandOptionType.NoValue);
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
            
            var noThinkTrimOption = cmd.Option("--no-think-trim",
                isZh ? "禁用 ThinkTrim。" : "Disable ThinkTrim",
                CommandOptionType.NoValue);
            var noTokenOption = cmd.Option("--no-token",
                isZh ? "禁用 API 密钥验证。" : "Disable API key verification",
                CommandOptionType.NoValue);
            var noDisableModelManageOption = cmd.Option("--use-model-manage",
                isZh ? "允许通过 API 进行模型管理。" : "Enable model management via API",
                CommandOptionType.NoValue);
            var usePublicPath = cmd.Option("--use-model-info-public-path",
                isZh ? "允许无需 APIKEY 通过 API 查看模型信息。" : "Allows get model information via API without APIKEY",
                CommandOptionType.NoValue);
            var useCorsAny = cmd.Option("--use-cors-any",
                isZh ? "允许跨域请求。" : "Allow cross-origin requests",
                CommandOptionType.NoValue);
            var useRateLimit = cmd.Option("--use-rate-limit",
                isZh ? "启用请求速率限制。(请在 ipratelimiting.json 中设置)" : "Enable request rate limiting (with ipratelimiting.json)",
                CommandOptionType.NoValue);
            var numCtxOption = cmd.Option<int>("--num-ctx <NumCtx>",
                isZh ? "设置每次请求的上下文数量。" : "Set the number of contexts per request",
                CommandOptionType.SingleOrNoValue);
            var useInputSecurityOption = cmd.Option("--use-input-security",
                isZh ? "启用输入内容安全检查。" : "Enable input security",
                CommandOptionType.NoValue);
            var riskModelOption = cmd.Option("--risk-model <RiskModel>",
                isZh ? "设置风险识别模型。" : "Set the input security risk model",
                CommandOptionType.SingleOrNoValue);
            var riskModelPromptOption = cmd.Option("--risk-model-prompt <RiskModelPrompt>",
                isZh ? "设置风险识别模型提示词。" : "Set the input security risk model prompt",
                CommandOptionType.SingleOrNoValue);
            var riskKeywordsOption = cmd.Option("--risk-keywords <RiskKeywords>",
                isZh ? "设置风险识别模型风险关键词。" : "Set the input security risk model keywords",
                CommandOptionType.MultipleValue);

            cmd.OnExecute(() =>
            {
                if (noTokenOption.HasValue()) UseToken = false;
                if (usePublicPath.HasValue()) UsePublicPath = true;
                if (useCorsAny.HasValue()) UseCorsAny = true;
                if (noDisableModelManageOption.HasValue()) NoModelManagePath = false;
                if (logOption.HasValue()) UseLog = true;
                if (useRateLimit.HasValue()) UseRateLimiting = true;
                if (noThinkTrimOption.HasValue()) UseThinkTrim = false;
                if (useInputSecurityOption.HasValue()) UseInputSecurity = true;

                if (riskModelOption.HasValue()) RiskModel = riskModelOption.Value();
                if (riskModelPromptOption.HasValue()) RiskModelPrompt = riskModelPromptOption.Value();
                if (riskKeywordsOption.Values.Count > 0)
                    RiskKeywordsList.AddRange(riskKeywordsOption.Values.ToList().Select(x => x.ToUpper()));

                if (ipOption.HasValue()) ListenUrl = ipOption.Value();
                if (targetOption.HasValue()) TargetUrl = targetOption.Value();
                if (hostOption.HasValue()) Hostname = hostOption.Value();
                if (numCtxOption.HasValue()) NumCtx = numCtxOption.ParsedValue;
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

                var config = new ConfigurationBuilder().AddJsonFile("ipratelimiting.json").Build();
                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddProxy(httpClientBuilder =>
                            httpClientBuilder.ConfigureHttpClient(client =>
                                client.Timeout = TimeSpan.FromMinutes(5)));

                        try
                        {
                            if (!UseRateLimiting) return;
                            services.AddMemoryCache();
                            services.Configure<IpRateLimitOptions>(config.GetSection("IpRateLimiting"));
                            services.AddInMemoryRateLimiting();
                            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
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
                        if (UseRateLimiting) app.UseMiddleware<IpRateLimitMiddleware>().UseIpRateLimiting();
                        app.Use(async (context, next) =>
                        {
                            if (UseCorsAny)
                            {
                                context.Response.Headers.TryAdd("Access-Control-Allow-Origin", "*");
                                context.Response.Headers.TryAdd("Access-Control-Allow-Methods", "*");
                                context.Response.Headers.TryAdd("Access-Control-Allow-Headers", "*");
                                context.Response.Headers.TryAdd("Access-Control-Allow-Credentials", "*");
                            }

                            if (!string.IsNullOrWhiteSpace(Hostname) &&
                                !string.Equals(context.Request.Host.Host, Hostname,
                                    StringComparison.CurrentCultureIgnoreCase))
                            {
                                context.Response.StatusCode = 404;
                                context.Response.ContentType = "text/plain";
                                await context.Response.WriteAsync("Host Not Found");
                                return;
                            }

                            if (NoModelManagePath && ModelManagePathList.Contains(context.Request.Path.ToString().ToLower().Trim()))
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
                                context.Items["Token"] = "sk-public";
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
                                    try
                                    {
                                        Console.WriteLine(context.Connection.RemoteIpAddress + ":" +
                                                          context.Request.Method.ToUpper() + ":" +
                                                          context.Request.PathBase + context.Request.Path);

                                        if (context.Request.Method.ToUpper() == "POST")
                                        {
                                            var jBody = JObject.Parse(await new StreamReader(context.Request.Body).ReadToEndAsync());

                                            if (NumCtx != -1 && context.Request.Path == "/chat" &&
                                                context.Request.PathBase == "/api")
                                                jBody["num_ctx"] = NumCtx;

                                            if (UseThinkTrim && jBody.TryGetValue("messages", out var msgs) &&
                                                msgs is JArray messages)
                                            {
                                                foreach (var item in messages)
                                                    item["content"] = item["content"]
                                                        ?.ToString().Split("</think>").LastOrDefault()?.Trim();
                                                jBody["messages"] = messages;
                                            }

                                            if (UseLog) Console.WriteLine(jBody.ToString());

                                            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jBody.ToString()));
                                            context.Request.ContentLength = context.Request.Body.Length;
                                        }

                                        var response = await context
                                            .ForwardTo(new Uri(TargetUrl + context.Request.PathBase)).Send();
                                        response.Headers.Add("X-Forwarder-By", "MondrianGateway/Lite");
                                        return response;
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                        return await context
                                            .ForwardTo(new Uri(TargetUrl + context.Request.PathBase)).Send(); ;
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
