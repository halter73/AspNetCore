// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Internal;

#nullable enable

namespace Microsoft.AspNetCore.Http;

internal static class EndpointMetadataPopulator
{
    private static readonly MethodInfo PopulateMetadataForParameterMethod = typeof(EndpointMetadataPopulator).GetMethod(nameof(PopulateMetadataForParameter), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo PopulateMetadataForEndpointMethod = typeof(EndpointMetadataPopulator).GetMethod(nameof(PopulateMetadataForEndpoint), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static object?[]? PopulateMetadata(MethodInfo methodInfo, EndpointBuilder builder, IEnumerable<ParameterInfo> parameters, object?[]? populateMetadataArgs = null)
    {
        // Get metadata from parameter types
        foreach (var parameter in parameters)
        {
            if (typeof(IEndpointParameterMetadataProvider).IsAssignableFrom(parameter.ParameterType))
            {
                // Parameter type implements IEndpointParameterMetadataProvider
                populateMetadataArgs ??= new object[2];
                populateMetadataArgs[0] = parameter;
                populateMetadataArgs[1] = builder;
                PopulateMetadataForParameterMethod.MakeGenericMethod(parameter.ParameterType).Invoke(null, populateMetadataArgs);
            }

            if (typeof(IEndpointMetadataProvider).IsAssignableFrom(parameter.ParameterType))
            {
                // Parameter type implements IEndpointMetadataProvider
                populateMetadataArgs ??= new object[2];
                populateMetadataArgs[0] = methodInfo;
                populateMetadataArgs[1] = builder;
                PopulateMetadataForEndpointMethod.MakeGenericMethod(parameter.ParameterType).Invoke(null, populateMetadataArgs);
            }
        }

        // Get metadata from return type
        var returnType = methodInfo.ReturnType;
        if (AwaitableInfo.IsTypeAwaitable(returnType, out var awaitableInfo))
        {
            returnType = awaitableInfo.ResultType;
        }

        if (returnType is not null && typeof(IEndpointMetadataProvider).IsAssignableFrom(returnType))
        {
            // Return type implements IEndpointMetadataProvider
            populateMetadataArgs ??= new object[2];
            populateMetadataArgs[0] = methodInfo;
            populateMetadataArgs[1] = builder;
            PopulateMetadataForEndpointMethod.MakeGenericMethod(returnType).Invoke(null, populateMetadataArgs);
        }

        return populateMetadataArgs;
    }

    private static void PopulateMetadataForParameter<T>(ParameterInfo parameter, EndpointBuilder builder)
        where T : IEndpointParameterMetadataProvider
    {
        T.PopulateMetadata(parameter, builder);
    }

    private static void PopulateMetadataForEndpoint<T>(MethodInfo method, EndpointBuilder builder)
        where T : IEndpointMetadataProvider
    {
        T.PopulateMetadata(method, builder);
    }
}
