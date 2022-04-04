// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing;

internal class ModelEndpointDataSource : EndpointDataSource, IGroupEndpointDataSource
{
    private readonly List<DefaultEndpointConventionBuilder> _endpointConventionBuilders = new();
    private readonly List<IEndpointConventionBuilder> _wrappedConventionBuilders = new(); 

    public void AddEndpointBuilder(DefaultEndpointConventionBuilder builder, IEndpointConventionBuilder wrappedBuilder)
    {
        _endpointConventionBuilders.Add(builder);
        _wrappedConventionBuilders.Add(wrappedBuilder);
    }

    public override IChangeToken GetChangeToken()
    {
        return NullChangeToken.Singleton;
    }

    public override IReadOnlyList<Endpoint> Endpoints => _endpointConventionBuilders.Select(e => e.Build()).ToArray();

    public IEnumerable<IEndpointConventionBuilder> ConventionBuilders => _wrappedConventionBuilders;

    // for testing
    internal IEnumerable<EndpointBuilder> EndpointBuilders => _endpointConventionBuilders.Select(b => b.EndpointBuilder);
}
