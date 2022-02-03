// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Hosting;

// This exists solely to bootstrap the configuration
internal sealed class BootstrapHostBuilder : IHostBuilder
{
    private readonly HostApplicationBuilder _builder;

    private readonly List<Action<IConfigurationBuilder>> _configureHostActions = new();
    private readonly List<Action<HostBuilderContext, IConfigurationBuilder>> _configureAppActions = new();
    private readonly List<Action<HostBuilderContext, IServiceCollection>> _configureServicesActions = new();

    public BootstrapHostBuilder(HostApplicationBuilder builder)
    {
        _builder = builder;

        Context = new HostBuilderContext(builder.HostBuilder.Properties)
        {
            HostingEnvironment = builder.Environment,
            Configuration = builder.Configuration,
        };
    }

    public IDictionary<object, object> Properties => _builder.HostBuilder.Properties;
    public HostBuilderContext Context { get; }

    public IHost Build()
    {
        // HostingHostBuilderExtensions.ConfigureDefaults should never call this.
        throw new InvalidOperationException();
    }

    public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _configureAppActions.Add(configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
        return this;
    }

    public IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate)
    {
        throw new InvalidOperationException();
    }

    public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
    {
        _configureHostActions.Add(configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
        return this;
    }

    public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        // HostingHostBuilderExtensions.ConfigureDefaults calls this via ConfigureLogging
        _configureServicesActions.Add(configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
        return this;
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
    {
        throw new InvalidOperationException();
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory) where TContainerBuilder : notnull
    {
        throw new InvalidOperationException();
    }

    public void RunDefaultCallbacks()
    {
        foreach (var configureHostAction in _configureHostActions)
        {
            configureHostAction(_builder.Configuration);
        }

        // ConfigureAppConfiguration cannot modify the host configuration because doing so could
        // change the environment, content root and application name which is not allowed at this stage.
        foreach (var configureAppAction in _configureAppActions)
        {
            configureAppAction(Context, _builder.Configuration);
        }

        foreach (var configureServicesAction in _configureServicesActions)
        {
            configureServicesAction(Context, _builder.Services);
        }
    }
}
