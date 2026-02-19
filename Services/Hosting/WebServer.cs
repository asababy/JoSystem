using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace JoSystem.Services.Hosting
{
    public class WebServer
    {
        private readonly string _rootPath;
        private readonly string _tempZipPath;

        private IHost _host;
        private readonly SemaphoreSlim _serverLock = new(1, 1);
        private readonly WebSocketManager _webSocketManager = new();
        private CancellationTokenSource _heartbeatCts;

        private bool _isRunning;
        public bool IsRunning => _isRunning;

        public WebServer(string rootPath)
        {
            _rootPath = Path.GetFullPath(rootPath);
            _tempZipPath = Path.Combine(Path.GetTempPath(), "JoSystemTempZips");
            Directory.CreateDirectory(_tempZipPath);
            _webSocketManager.ClientMessageReceived += OnClientMessageAsync;
        }

        public async Task StartAsync()
        {
            await _serverLock.WaitAsync();
            try
            {
                if (_isRunning) await StopInternalAsync();

                int httpPort = ConfigService.Current.HttpPort;
                int httpsPort = ConfigService.Current.HttpsPort;
                bool enableHttps = ConfigService.Current.EnableHttps;

                await WaitPortReleased(httpPort);
                if (enableHttps)
                {
                    await WaitPortReleased(httpsPort);
                }

                _host = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel(o =>
                        {
                            o.ListenAnyIP(httpPort);

                            if (enableHttps)
                            {
                                o.ListenAnyIP(httpsPort, listenOptions =>
                                {
                                    var cert = CertificateHelper.GetOrGenerateCertificate();
                                    listenOptions.UseHttps(cert);
                                });
                            }

                            o.Limits.MaxRequestBodySize = long.MaxValue;
                        });

                        webBuilder.ConfigureServices(services =>
                        {
                            var mvcBuilder = services.AddControllers();

                            var baseDir = AppContext.BaseDirectory;
                            var qmSystemPath = Path.Combine(baseDir, "QMSystem.dll");
                            if (File.Exists(qmSystemPath))
                            {
                                try
                                {
                                    var qmAssembly = Assembly.LoadFrom(qmSystemPath);
                                    mvcBuilder.PartManager.ApplicationParts.Add(new AssemblyPart(qmAssembly));
                                }
                                catch
                                {
                                }
                            }

                            services.AddEndpointsApiExplorer();
                            services.AddSwaggerGen(c =>
                            {
                                c.SwaggerDoc("v1", new OpenApiInfo
                                {
                                    Title = "JoSystem API",
                                    Version = "v1"
                                });
                            });
                        });

                        webBuilder.Configure(app => ConfigureWebApplication(app));
                    }).Build();

                await _host.StartAsync();
                _isRunning = true;

                StartHeartbeat();
                await BroadcastServerStatus(true);
            }
            finally { _serverLock.Release(); }
        }

        private void ConfigureWebApplication(IApplicationBuilder app)
        {
            app.UseWebSockets();

            ConfigureProtectedApiMiddleware(app);
            ConfigureSwagger(app);

            app.Map("/ws", ws => ws.Run(ctx => _webSocketManager.HandleConnectionAsync(ctx)));

            app.UseRouting();
            app.UseEndpoints(e =>
            {
                e.MapControllers();
            });

            app.Run(ServeRequest);
        }

        private static void ConfigureSwagger(IApplicationBuilder app)
        {
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "mapa/swagger/{documentName}/swagger.json";
            });

            app.Map("/mapa/swagger", swaggerApp =>
            {
                swaggerApp.Use(async (ctx, next) =>
                {
                    if (!ConfigService.Current.EnableSwagger ||
                        !IpWhitelistHelper.IsAllowed(ctx.Connection.RemoteIpAddress, ConfigService.Current.SwaggerIpWhitelist))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await ctx.Response.WriteAsync("Swagger is not allowed from this IP.");
                        return;
                    }

                    await next();
                });

                swaggerApp.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("v1/swagger.json", "JoSystem API v1");
                    c.RoutePrefix = string.Empty;
                });
            });
        }


        private static void ConfigureProtectedApiMiddleware(IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value;
                var protectedPaths = ConfigService.Current.ProtectedApiPaths;

                if (path != null && protectedPaths != null)
                {
                    foreach (var protectedPath in protectedPaths)
                    {
                        if (path.StartsWith(protectedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (context.Request.Cookies["IsLoggedIn"] != "true")
                            {
                                context.Response.StatusCode = 401;
                                context.Response.ContentType = "application/json; charset=utf-8";
                                await context.Response.WriteAsJsonAsync(new { success = false, message = "未授权，请先登录" });
                                return;
                            }
                            break;
                        }
                    }
                }

                await next();
            });
        }

        private async Task OnClientMessageAsync(string msg)
        {
            if (msg.Contains("requestRefresh"))
            {
                await _webSocketManager.SendToAllAsync(new
                {
                    type = "filesChanged",
                    data = new { reason = "manual" },
                    time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!_heartbeatCts.IsCancellationRequested)
                {
                    await _webSocketManager.SendToAllAsync(new
                    {
                        type = "heartbeat",
                        time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                    await Task.Delay(5000);
                }
            });
        }

        public async Task StopAsync()
        {
            await _serverLock.WaitAsync();
            try { await StopInternalAsync(); }
            finally { _serverLock.Release(); }
        }

        private async Task StopInternalAsync()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _heartbeatCts?.Cancel();

            await BroadcastServerStatus(false);

            try { await _host?.StopAsync(TimeSpan.FromSeconds(2)); } catch { }
            _webSocketManager.Cleanup();
            _host?.Dispose();
            _host = null;
        }

        private async Task BroadcastServerStatus(bool running)
        {
            await _webSocketManager.SendToAllAsync(new
            {
                type = "serverStatus",
                data = new {
                    running,
                    port = ConfigService.Current.HttpPort,
                    httpsPort = ConfigService.Current.HttpsPort,
                    enableHttps = ConfigService.Current.EnableHttps,
                    rootPath = _rootPath
                },
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        private async Task WaitPortReleased(int port)
        {
            for (int i = 0; i < 40; i++)
            {
                if (!IsPortInUse(port)) return;
                await Task.Delay(50);
            }
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                using var l = new TcpListener(IPAddress.Loopback, port);
                l.Start();
                return false;
            }
            catch { return true; }
        }

        private async Task ServeRequest(HttpContext ctx)
        {
            var path = ctx.Request.Path.Value;
            if (path == null) path = "/";

            if (!string.Equals(path, "/", StringComparison.Ordinal))
            {
                if (await TryServeEmbeddedResource(ctx, path))
                {
                    return;
                }
            }

            await ServeIndexHtml(ctx);
        }

        private static async Task<bool> TryServeEmbeddedResource(HttpContext ctx, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            if (path.StartsWith("/")) path = path[1..];

            var resourcePath = path.Replace('/', '.');

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = asm.GetName().Name;
                var fullName = $"{asmName}.WebPages.{resourcePath}";

                var stream = asm.GetManifestResourceStream(fullName);
                if (stream == null) continue;

                ctx.Response.ContentType = GetContentType(path);
                using (stream)
                {
                    await stream.CopyToAsync(ctx.Response.Body);
                    return true;
                }
            }

            return false;
        }

        private static string GetContentType(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }

        private async Task ServeIndexHtml(HttpContext ctx)
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("JoSystem.WebPages.index.html");
            if (stream != null) await stream.CopyToAsync(ctx.Response.Body);
            else await ctx.Response.WriteAsync("Index.html not found as Embedded Resource.");
        }
    }
}
