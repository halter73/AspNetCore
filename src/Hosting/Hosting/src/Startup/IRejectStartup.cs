// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Hosting
{
    /// <summary>
    /// A marker interface used to indicate the <see cref="IHostBuilder"/> or <see cref="IWebHostBuilder"/>
    /// does not support any of the following extension methods:
    /// * <see cref="WebHostBuilderExtensions.Configure(IWebHostBuilder, Action{AspNetCore.Builder.IApplicationBuilder})"/>
    /// * <see cref="WebHostBuilderExtensions.UseStartup(IWebHostBuilder, Type)"/>
    /// * <see cref="GenericHostWebHostBuilderExtensions.ConfigureWebHost(IHostBuilder, Action{IWebHostBuilder})"/>
    /// </summary>
    public interface IRejectStartup
    {
    }
}
