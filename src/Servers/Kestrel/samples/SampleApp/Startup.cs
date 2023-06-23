// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleApp;

public class Startup
{
    public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Default");

        app.UseClientCertBuffering();

        // Add an exception handler that prevents throwing due to large request body size
        app.Use(async (context, next) =>
        {
            // Limit the request body to 1kb
            context.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = 1024;

            try
            {
                await next.Invoke(context);
            }
            catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413RequestEntityTooLarge) { }
        });

        app.Run(async context =>
        {
            // Drain the request body
            // await context.Request.Body.CopyToAsync(Stream.Null);

            var cert = await context.Connection.GetClientCertificateAsync();

            var connectionFeature = context.Connection;
            logger.LogDebug($"Peer: {connectionFeature.RemoteIpAddress?.ToString()}:{connectionFeature.RemotePort}"
                + $"{Environment.NewLine}"
                + $"Sock: {connectionFeature.LocalIpAddress?.ToString()}:{connectionFeature.LocalPort}"
                + $"{Environment.NewLine}"
                + cert);

            var response = $"hello, world{Environment.NewLine}";
            context.Response.ContentLength = response.Length;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(response);
        });
    }

    public static Task Main(string[] args)
    {
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Console.WriteLine("Unobserved exception: {0}", e.Exception);
        };

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .UseKestrel((context, options) =>
                    {
                        if (context.HostingEnvironment.IsDevelopment())
                        {
                            ShowConfig(context.Configuration);
                        }

                        var basePort = context.Configuration.GetValue<int?>("BASE_PORT") ?? 5000;

                        options.ConfigureHttpsDefaults(httpsOptions =>
                        {
                            httpsOptions.SslProtocols = SslProtocols.Tls12;

                            if (!OperatingSystem.IsMacOS())
                            {
                                // Delayed client certificate negotiation is not supported on macOS.
                                httpsOptions.ClientCertificateMode = ClientCertificateMode.DelayCertificate;
                            }
                        });

                        options.Listen(IPAddress.Loopback, basePort, listenOptions =>
                        {
                            // Uncomment the following to enable Nagle's algorithm for this endpoint.
                            //listenOptions.NoDelay = false;

                            listenOptions.UseConnectionLogging();
                        });

                        options.Listen(IPAddress.Loopback, basePort + 1, listenOptions =>
                        {
                            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
                            listenOptions.UseHttps();
                            listenOptions.UseConnectionLogging();
                        });

                        options.ListenLocalhost(basePort + 2, listenOptions =>
                        {
                            // Use default dev cert
                            listenOptions.UseHttps();
                        });

                        options.ListenAnyIP(basePort + 3);

                        options.ListenAnyIP(basePort + 4, listenOptions =>
                        {
                            listenOptions.UseHttps(StoreName.My, "localhost", allowInvalid: true);
                        });

                        options.ListenAnyIP(basePort + 5, listenOptions =>
                        {
                            var localhostCert = CertificateLoader.LoadFromStoreCert("localhost", "My", StoreLocation.CurrentUser, allowInvalid: true);

                            listenOptions.UseHttps((stream, clientHelloInfo, state, cancellationToken) =>
                            {
                                // Here you would check the name, select an appropriate cert, and provide a fallback or fail for null names.
                                var serverName = clientHelloInfo.ServerName;
                                if (serverName != null && serverName != "localhost")
                                {
                                    throw new AuthenticationException($"The endpoint is not configured for server name '{clientHelloInfo.ServerName}'.");
                                }

                                return new ValueTask<SslServerAuthenticationOptions>(new SslServerAuthenticationOptions
                                {
                                    ServerCertificate = localhostCert
                                });
                            }, state: null);
                        });

                        options
                            .Configure()
                            .Endpoint(IPAddress.Loopback, basePort + 6)
                            .LocalhostEndpoint(basePort + 7)
                            .Load();

                        // reloadOnChange: true is the default
                        options
                        .Configure(context.Configuration.GetSection("Kestrel"), reloadOnChange: true)
                        .Endpoint("NamedEndpoint", opt =>
                        {

                        })
                        .Endpoint("NamedHttpsEndpoint", opt =>
                        {
                            opt.HttpsOptions.SslProtocols = SslProtocols.Tls12;
                        });

                        options.UseSystemd();

                        // The following section should be used to demo sockets
                        //options.ListenUnixSocket("/tmp/kestrel-test.sock");
                    })
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseStartup<Startup>();
            })
            .ConfigureLogging((_, factory) =>
            {
                factory.SetMinimumLevel(LogLevel.Debug);
                factory.AddConsole();
            })
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            });

        return hostBuilder.Build().RunAsync();
    }

    private static void ShowConfig(IConfiguration config)
    {
        foreach (var pair in config.GetChildren())
        {
            Console.WriteLine($"{pair.Path} - {pair.Value}");
            ShowConfig(pair);
        }
    }

    private class TimingPipeWriter : PipeWriter
    {
        private readonly ConnectionContext _connection;
        private readonly double _minBytesPerSecond;
        private readonly PipeWriter _inner;

        private DateTimeOffset? _connectionStartTime;
        private DateTimeOffset? _windowStartTime;
        private long _bytesSentInWindow;
        private TimeSpan _timeSpentFlushingInWindow;

        public TimingPipeWriter(ConnectionContext connection, double minBytesPerSecond)
        {
            _connection = connection;
            _minBytesPerSecond = minBytesPerSecond;
            _inner = connection.Transport.Output;
        }

        public override void Advance(int bytes)
        {
            _bytesSentInWindow += bytes;
            _inner.Advance(bytes);
        }

        public override void CancelPendingFlush()
        {
            _inner.CancelPendingFlush();
        }

        public override void Complete(Exception exception = null)
        {
            _inner.Complete(exception);
        }

        public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            var flushStartTime = DateTimeOffset.UtcNow;
            var connectionStartTime = _connectionStartTime ??= flushStartTime;
            var windowStartTime = _windowStartTime ??= flushStartTime;

            var flushResult = await _inner.FlushAsync(cancellationToken);

            var flushEndTime = DateTimeOffset.UtcNow;
            _timeSpentFlushingInWindow += flushEndTime - flushStartTime;

            if (flushEndTime - connectionStartTime > TimeSpan.FromSeconds(5) && _timeSpentFlushingInWindow > TimeSpan.FromSeconds(1))
            {
                if (_bytesSentInWindow / _timeSpentFlushingInWindow.TotalSeconds < _minBytesPerSecond)
                {
                    _connection.Abort(new ConnectionAbortedException($"The response rate was not at least {_minBytesPerSecond} bytes/sec."));
                    return flushResult;
                }
            }

            if (flushEndTime - windowStartTime > TimeSpan.FromSeconds(5))
            {
                // Don't start the next window until we're sure there's still something we're trying to write.
                _windowStartTime = null;
                _bytesSentInWindow = 0;
                _timeSpentFlushingInWindow = TimeSpan.Zero;
            }

            return flushResult;
        }

        public override Memory<byte> GetMemory(int sizeHint = 0)
        {
            return _inner.GetMemory(sizeHint);
        }

        public override Span<byte> GetSpan(int sizeHint = 0)
        {
            return _inner.GetSpan(sizeHint);
        }
    }

    private static Pipe CreateTimingPipe(ConnectionContext connection, long maxWriteBufferSize)
    {
        var pipeOptions = new PipeOptions(
            pool: connection.Features.GetRequiredFeature<IMemoryPoolFeature>().MemoryPool,
            readerScheduler: PipeScheduler.Inline,
            writerScheduler: PipeScheduler.Inline,
            pauseWriterThreshold: maxWriteBufferSize,
            resumeWriterThreshold: maxWriteBufferSize / 2,
            useSynchronizationContext: false);

        return new Pipe(pipeOptions);
    }

    private static async Task CopyAndTimeOutputAsync(ConnectionContext connection, PipeReader outputReader, PipeWriter outputWriter, double minBytesPerSecond)
    {
        const int MinAllocBufferSize = 2048;

        const int MaxWriteSize = 64 * 1024;
        const int NumWindows = 10;
        const int WindowSizeSeconds = 5;

        var heartbeat = connection.Features.GetRequiredFeature<IConnectionHeartbeatFeature>();

        heartbeat.OnHeartbeat(_ =>
        {

        }, null);

        while (!connection.ConnectionClosed.IsCancellationRequested)
        {
            var readResult = await outputReader.ReadAsync();

            if ((readResult.IsCompleted && readResult.Buffer.Length == 0) || readResult.IsCanceled)
            {
                // FIN
                break;
            }

            var outputBuffer = outputWriter.GetMemory(MinAllocBufferSize);

            var copyAmount = (int)Math.Min(outputBuffer.Length, readResult.Buffer.Length);
            var bufferSlice = readResult.Buffer.Slice(0, copyAmount);

            bufferSlice.CopyTo(outputBuffer.Span);

            outputWriter.Advance(copyAmount);

            var flushResult = await outputWriter.FlushAsync();

            outputReader.AdvanceTo(bufferSlice.End);

            if (flushResult.IsCompleted || flushResult.IsCanceled)
            {
                // flushResult should not be canceled.
                break;
            }
        }
    }

    private class DuplexPipe : IDuplexPipe
    {
        public PipeReader Input { get; set; } = null!;

        public PipeWriter Output { get; set; } = null!;
    }
}
