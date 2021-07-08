// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Hosting
{
    internal class HostingStartupContext
    {
        public object StartupKey { get; } = new();
        public object? StartupObject { get; set; }

        public AggregateException? HostingStartupErrors { get; set; }
        public HostingStartupWebHostBuilder? HostingStartupWebHostBuilder { get; set; }
    }
}
