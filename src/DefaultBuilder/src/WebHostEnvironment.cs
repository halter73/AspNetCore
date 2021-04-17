// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Builder
{
    internal class WebHostEnvironment : IWebHostEnvironment
    {
        private static readonly NullFileProvider NullFileProvider = new();

        private string _applicationName = default!;
        private string _environmentName = default!;
        private string _contentRootPath = default!;
        private string _webRootPath = default!;

        private bool _trackModifications;
        private bool _applicationNameModified;
        private bool _environmentNameModified;
        private bool _contentRootPathModified;
        private bool _webRootPathModified;

        public WebHostEnvironment(Assembly? callingAssembly)
        {
            ContentRootPath = Directory.GetCurrentDirectory();

            ApplicationName = (callingAssembly ?? Assembly.GetEntryAssembly())?.GetName()?.Name ?? string.Empty;
            EnvironmentName = Environments.Production;

            // This feels wrong, but HostingEnvironment also sets WebRoot to "default!".
            WebRootPath = default!;

            // Default to /wwwroot if it exists.
            var wwwroot = Path.Combine(ContentRootPath, "wwwroot");
            if (Directory.Exists(wwwroot))
            {
                WebRootPath = wwwroot;
            }

            ContentRootFileProvider = NullFileProvider;
            WebRootFileProvider = NullFileProvider;

            ResolveFileProviders(new Configuration());
        }

        public void ResolveFileProviders(IConfiguration configuration)
        {
            if (Directory.Exists(ContentRootPath))
            {
                ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
            }

            if (Directory.Exists(WebRootPath))
            {
                WebRootFileProvider = new PhysicalFileProvider(Path.Combine(ContentRootPath, WebRootPath));
            }

            if (this.IsDevelopment())
            {
                StaticWebAssetsLoader.UseStaticWebAssets(this, configuration);
            }
        }

        public void MirrorGenericWebHostEnvironment(IWebHostEnvironment genericWebHostEnvironment)
        {
            ApplicationName = genericWebHostEnvironment.ApplicationName;
            EnvironmentName = genericWebHostEnvironment.EnvironmentName;
            ContentRootPath = genericWebHostEnvironment.ContentRootPath;
            ContentRootFileProvider = genericWebHostEnvironment.ContentRootFileProvider;
            WebRootFileProvider = genericWebHostEnvironment.WebRootFileProvider;
        }

        public void StartTrackingModifications()
        {
            _trackModifications = true;
        }

        public void ReconcileWebHostEnvironments(IWebHostBuilder builder)
        {
            // Always reset ApplicationName on the builder because GenericWebHostBuilder.Configure() overrides the
            // ApplicationNName with the name of the assembly declaring the callback, "Microsoft.AspNetCore".

            if (_applicationNameModified || builder.GetSetting(WebHostDefaults.ApplicationKey) == Assembly.GetExecutingAssembly()?.GetName()?.Name)
            {
                builder.UseSetting(WebHostDefaults.ApplicationKey, ApplicationName);
            }

            // Use the derived default EnvironementName from the GenericWebHostBuilder which can pick up
            // config from "ASPNET_" Environment variables unless it has been custom-configured.
            if (_environmentNameModified)
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, _environmentName);
            }
            else if (builder.GetSetting(WebHostDefaults.EnvironmentKey) is string envName)
            {
                _environmentName = envName;
            }

            if (_contentRootPathModified)
            {
                builder.UseSetting(WebHostDefaults.ContentRootKey, _contentRootPath);
            }

            if (_webRootPathModified)
            {
                builder.UseSetting(WebHostDefaults.WebRootKey, _webRootPath);
            }
        }

         public string ApplicationName 
        {
            get => _applicationName;
            set
            {
                if (_trackModifications)
                {
                    _applicationNameModified = true;
                }

                _applicationName = value;
            }
        }

        public string EnvironmentName
        {
            get => _environmentName;
            set
            {
                if (_trackModifications)
                {
                    _environmentNameModified = true;
                }

                _environmentName = value;
            }
        }

        public string ContentRootPath
        {
            get => _contentRootPath;
            set
            {
                if (_trackModifications)
                {
                    _contentRootPathModified = true;
                }

                _contentRootPath = value;
            }
        }

        public string WebRootPath
        {
            get => _webRootPath;
            set
            {
                if (_trackModifications)
                {
                    _webRootPathModified = true;
                }

                _webRootPath = value;
            }
        }

        public IFileProvider ContentRootFileProvider { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
    }
}
