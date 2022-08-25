// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Mvc.ApplicationModels;

internal sealed class EndpointMetadataConvention : IActionModelConvention
{
    private readonly IServiceProvider _serviceProvider;

    public EndpointMetadataConvention(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Apply(ActionModel action)
    {
        ParameterInfo[]? parameters = null;
        object?[]? populateMetadataArgs = null;

        foreach (var selector in action.Selectors)
        {
            // The EndpointBuilder property is internal and this is the only place we reference it.
            // Since the SelectorModel can be newed up with an empty constructor, we have to wait until now to configure ApplicationServices.
            // However, cannot delay the creation of the internal EndpointBuilder because it backs the public EndpointMetadata property.
            selector.EndpointBuilder.ApplicationServices = _serviceProvider;

            parameters ??= action.ActionMethod.GetParameters();
            populateMetadataArgs = EndpointMetadataPopulator.PopulateMetadata(action.ActionMethod, selector.EndpointBuilder, parameters, populateMetadataArgs);
        }
    }
}
