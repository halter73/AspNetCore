// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Hosting
{
    public class GenericWebHostStartupContext
    {
        public object Key { get; } = new();
        public object? Object { get; set; }

        public AggregateException? Errors { get; set; }
        public IWebHostBuilder? WebHostBuilder { get; set; }
    }
}
