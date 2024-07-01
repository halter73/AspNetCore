// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Moq;

namespace Microsoft.AspNetCore.Components.Server.Circuits;

internal class TestCircuitHost : CircuitHost
{
    private TestCircuitHost(
        CircuitId circuitId,
        AsyncServiceScope scope,
        CircuitOptions options,
        CircuitClientProxy client,
        RemoteRenderer renderer,
        IReadOnlyList<ComponentDescriptor> descriptors,
        RemoteJSRuntime jsRuntime,
        RemoteNavigationManager navigationManager,
        TimeProvider timeProvider,
        CircuitHandler[] circuitHandlers,
        ILogger logger)
        : base(circuitId, scope, options, client, renderer, descriptors, jsRuntime, navigationManager, timeProvider, circuitHandlers, logger)
    {
    }

    public static CircuitHost Create(
        CircuitId? circuitId = null,
        AsyncServiceScope? serviceScope = null,
        RemoteRenderer remoteRenderer = null,
        TimeProvider timeProvider = null,
        IReadOnlyList<ComponentDescriptor> descriptors = null,
        CircuitHandler[] handlers = null,
        CircuitClientProxy clientProxy = null)
    {
        serviceScope = serviceScope ?? new AsyncServiceScope(Mock.Of<IServiceScope>());
        clientProxy = clientProxy ?? new CircuitClientProxy(Mock.Of<ISingleClientProxy>(), Guid.NewGuid().ToString());
        timeProvider = timeProvider ?? TimeProvider.System;
        var jsRuntime = new RemoteJSRuntime(Options.Create(new CircuitOptions()), Options.Create(new HubOptions<ComponentHub>()), Mock.Of<ILogger<RemoteJSRuntime>>());
        var navigationManager = new RemoteNavigationManager(Mock.Of<ILogger<RemoteNavigationManager>>());
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(services => services.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntime);
        var serverComponentDeserializer = Mock.Of<IServerComponentDeserializer>();

        remoteRenderer ??= new RemoteRenderer(
                serviceProvider.Object,
                NullLoggerFactory.Instance,
                new CircuitOptions(),
                clientProxy,
                serverComponentDeserializer,
                NullLogger.Instance,
                jsRuntime,
                new CircuitJSComponentInterop(new CircuitOptions()));

        handlers ??= [];
        return new TestCircuitHost(
            circuitId is null ? new CircuitId(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()) : circuitId.Value,
            serviceScope.Value,
            new CircuitOptions(),
            clientProxy,
            remoteRenderer,
            descriptors ?? [],
            jsRuntime,
            navigationManager,
            timeProvider,
            handlers,
            NullLogger<CircuitHost>.Instance);
    }
}
