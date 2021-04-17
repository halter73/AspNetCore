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

        private bool _recordDefaults = true;

        private string _environmentName = default!, _environmentNameDefault = default!;
        private string _contentRootPath = default!, _contentRootPathDefault = default!;
        private string _webRootPath = default!, _webRootPathDefault = default!;

        public WebHostEnvironment(Assembly? callingAssembly)
        {
            ContentRootPath = Directory.GetCurrentDirectory();

            ApplicationName = (callingAssembly ?? Assembly.GetEntryAssembly())?.GetName()?.Name ?? string.Empty;
            EnvironmentName = Environments.Production;

            // This feels wrong, but HostingEnvironment also sets WebRoot to "default!".

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

            if (Directory.Exists(_webRootPath))
            {
                WebRootFileProvider = new PhysicalFileProvider(Path.Combine(ContentRootPath, _webRootPath));
            }

            if (this.IsDevelopment())
            {
                StaticWebAssetsLoader.UseStaticWebAssets(this, configuration);
            }
        }

        public void StopRecordingDefaultSettings()
        {
            _recordDefaults = false;
        }

        public void ApplySettings(IWebHostBuilder builder)
        {
            // Always set ApplicationName on the builder because GenericWebHostBuilder.Configure() overrides the
            // ApplicationNName with the name of the assembly declaring the callback, "Microsoft.AspNetCore".
            builder.UseSetting(WebHostDefaults.ApplicationKey, ApplicationName);
            builder.UseSetting(WebHostDefaults.ContentRootKey, _contentRootPath);
            builder.UseSetting(WebHostDefaults.WebRootKey, _webRootPath);

            if (_environmentName != _environmentNameDefault)
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, _environmentName);
            }
            //else if (builder.GetSetting())
            if (_contentRootPath != _contentRootPathDefault)
            {
            }
            if (_webRootPath != _webRootPathDefault)
            {
            }
        }

        public IFileProvider ContentRootFileProvider { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }

        public string ApplicationName { get; set; }

        public string EnvironmentName
        {
            get => _environmentName;
            set
            {
                if (_recordDefaults)
                {
                    _environmentNameDefault = value;
                }

                _environmentName = value;
            }
        }

        public string ContentRootPath
        {
            get => _contentRootPath;
            set
            {
                if (_recordDefaults)
                {
                    _contentRootPathDefault = value;
                }

                _contentRootPath = value;
            }
        }


        public string WebRootPath
        {
            get => _webRootPath;
            set
            {
                if (_recordDefaults)
                {
                    _webRootPathDefault = value;
                }

                _webRootPath = value;
            }
        }
    }
}
