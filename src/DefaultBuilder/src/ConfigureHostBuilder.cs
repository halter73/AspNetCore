// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// A non-buildable <see cref="IHostBuilder"/> for <see cref="WebApplicationBuilder"/>.
/// Use <see cref="WebApplicationBuilder.Build"/> to build the <see cref="WebApplicationBuilder"/>.
/// </summary>
public sealed class ConfigureHostBuilder : IHostBuilder, ISupportsConfigureWebHost
{
    private readonly ConfigurationManager _configuration;
    private readonly IServiceCollection _services;
    private readonly HostBuilderContext _context;

    private readonly List<Action<HostBuilderContext, object>> _configureContainerActions = new();
    private IServiceProviderFactory<object>? _customServiceProviderFactory;

    internal ConfigureHostBuilder(
        HostBuilderContext context,
        ConfigurationManager configuration,
        IServiceCollection services,
        IServiceProviderFactory<object>? customServiceProviderFactory = null)
    {
        _configuration = configuration;
        _services = services;
        _context = context;
    }

    /// <inheritdoc />
    public IDictionary<object, object> Properties => _context.Properties;

    IHost IHostBuilder.Build()
    {
        throw new NotSupportedException($"Call {nameof(WebApplicationBuilder)}.{nameof(WebApplicationBuilder.Build)}() instead.");
    }

    /// <inheritdoc />
    public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        // Run these immediately so that they are observable by the imperative code
        configureDelegate(_context, _configuration);
        return this;
    }

    /// <inheritdoc />
    public IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate)
    {
        if (configureDelegate is null)
        {
            throw new ArgumentNullException(nameof(configureDelegate));
        }

        _configureContainerActions.Add((context, containerBuilder) => configureDelegate(context, (TContainerBuilder)containerBuilder));

        return this;
    }

    /// <inheritdoc />
    public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
    {
        var previousApplicationName = _configuration[HostDefaults.ApplicationKey];
        // Use the real content root so we can compare paths
        var previousContentRoot = HostingPathResolver.ResolvePath(_context.HostingEnvironment.ContentRootPath);
        var previousContentRootConfig = _configuration[HostDefaults.ContentRootKey];
        var previousEnvironment = _configuration[HostDefaults.EnvironmentKey];

        // Run these immediately so that they are observable by the imperative code
        configureDelegate(_configuration);

        // Disallow changing any host settings this late in the cycle, the reasoning is that we've already loaded the default configuration
        // and done other things based on environment name, application name or content root.
        if (!string.Equals(previousApplicationName, _configuration[HostDefaults.ApplicationKey], StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"The application name changed from \"{previousApplicationName}\" to \"{_configuration[HostDefaults.ApplicationKey]}\". Changing the host configuration using WebApplicationBuilder.Host is not supported. Use WebApplication.CreateBuilder(WebApplicationOptions) instead.");
        }

        if (!string.Equals(previousContentRootConfig, _configuration[HostDefaults.ContentRootKey], StringComparison.OrdinalIgnoreCase)
            && !string.Equals(previousContentRoot, HostingPathResolver.ResolvePath(_configuration[HostDefaults.ContentRootKey]), StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"The content root changed from \"{previousContentRoot}\" to \"{HostingPathResolver.ResolvePath(_configuration[HostDefaults.ContentRootKey])}\". Changing the host configuration using WebApplicationBuilder.Host is not supported. Use WebApplication.CreateBuilder(WebApplicationOptions) instead.");
        }

        if (!string.Equals(previousEnvironment, _configuration[HostDefaults.EnvironmentKey], StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"The environment changed from \"{previousEnvironment}\" to \"{_configuration[HostDefaults.EnvironmentKey]}\". Changing the host configuration using WebApplicationBuilder.Host is not supported. Use WebApplication.CreateBuilder(WebApplicationOptions) instead.");
        }

        return this;
    }

    /// <inheritdoc />
    public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        // Run these immediately so that they are observable by the imperative code
        configureDelegate(_context, _services);
        return this;
    }

    /// <inheritdoc />
    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        _customServiceProviderFactory = new CustomServiceFactoryAdapter<TContainerBuilder>(_context, _ => factory, _configureContainerActions);
        return this;
    }

    /// <inheritdoc />
    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory) where TContainerBuilder : notnull
    {
        _customServiceProviderFactory = new CustomServiceFactoryAdapter<TContainerBuilder>(_context, factory ?? throw new ArgumentNullException(nameof(factory)), _configureContainerActions);
        return this;
    }

    IHostBuilder ISupportsConfigureWebHost.ConfigureWebHost(Action<IWebHostBuilder> configure, Action<WebHostBuilderOptions> configureOptions)
    {
        throw new NotSupportedException("ConfigureWebHost() is not supported by WebApplicationBuilder.Host. Use the WebApplication returned by WebApplicationBuilder.Build() instead.");
    }

    internal IServiceProviderFactory<object>? GetCustomServiceProviderFactory()
    {
        if (_customServiceProviderFactory is null && _configureContainerActions.Count > 0)
        {
            // UseServiceProviderFactory wasn't called, so the _configureContanerActions must be for IServiceCollection.
            // If not, the cast from IServiceCollection to whatever the action expects will fail like with HostBuilder.
            foreach (var action in _configureContainerActions)
            {
                action(_context, _services);
            }
        }

        return _customServiceProviderFactory;
    }

    private sealed class CustomServiceFactoryAdapter<TContainerBuilder> : IServiceProviderFactory<object> where TContainerBuilder : notnull
    {
        private IServiceProviderFactory<TContainerBuilder>? _serviceProviderFactory;
        private readonly HostBuilderContext _context;
        private readonly Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> _factoryResolver;
        private readonly List<Action<HostBuilderContext, object>> _configureContainerActions;

        public CustomServiceFactoryAdapter(
            HostBuilderContext context,
            Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factoryResolver,
            List<Action<HostBuilderContext, object>> configureContainerAdapters)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _factoryResolver = factoryResolver ?? throw new ArgumentNullException(nameof(factoryResolver));
            _configureContainerActions = configureContainerAdapters ?? throw new ArgumentNullException(nameof(configureContainerAdapters));
        }

        public object CreateBuilder(IServiceCollection services)
        {
            if (_serviceProviderFactory is null)
            {
                _serviceProviderFactory = _factoryResolver(_context);

                if (_serviceProviderFactory is null)
                {
                    throw new InvalidOperationException();
                }
            }

            var containerBuilder = _serviceProviderFactory.CreateBuilder(services);

            foreach (var action in _configureContainerActions)
            {
                action(_context, containerBuilder);
            }

            return containerBuilder;
        }

        public IServiceProvider CreateServiceProvider(object containerBuilder)
        {
            if (_serviceProviderFactory is null)
            {
                throw new InvalidOperationException();
            }

            return _serviceProviderFactory.CreateServiceProvider((TContainerBuilder)containerBuilder);
        }
    }
}
