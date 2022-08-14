// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace MvcSandbox.Controllers;

public class MetadataProviderController
{
    public AddsCustomEndpointMetadataActionResult ActionWithParameterMetadata(AddsCustomParameterMetadata param1) => new();
    public AddsCustomEndpointMetadataActionResult ActionWithRemovalFromParameterMetadata(RemovesAcceptsParameterMetadata param1) => new();
    public AddsCustomEndpointMetadataActionResult ActionWithRemovalFromParameterEndpointMetadata(RemovesAcceptsParameterEndpointMetadata param1) => new();

    [HttpGet("selector1")]
    [HttpGet("selector2")]
    public AddsCustomEndpointMetadataActionResult MultipleSelectorsActionWithParameterMetadata(AddsCustomParameterMetadata param1) => new();

    public AddsCustomEndpointMetadataResult ActionWithMetadataInResult() => new();

    public ValueTask<AddsCustomEndpointMetadataResult> ActionWithMetadataInValueTaskOfResult()
        => ValueTask.FromResult<AddsCustomEndpointMetadataResult>(new());

    public Task<AddsCustomEndpointMetadataResult> ActionWithMetadataInTaskOfResult()
        => Task.FromResult<AddsCustomEndpointMetadataResult>(new());

    [HttpGet("selector1")]
    [HttpGet("selector2")]
    public AddsCustomEndpointMetadataActionResult MultipleSelectorsActionWithMetadataInActionResult() => new();

    public AddsCustomEndpointMetadataActionResult ActionWithMetadataInActionResult() => new();

    public ValueTask<AddsCustomEndpointMetadataActionResult> ActionWithMetadataInValueTaskOfActionResult()
        => ValueTask.FromResult<AddsCustomEndpointMetadataActionResult>(new());

    public Task<AddsCustomEndpointMetadataActionResult> ActionWithMetadataInTaskOfActionResult()
        => Task.FromResult<AddsCustomEndpointMetadataActionResult>(new());

    public RemovesAcceptsMetadataResult ActionWithNoAcceptsMetadataInResult() => new();

    public ValueTask<RemovesAcceptsMetadataResult> ActionWithNoAcceptsMetadataInValueTaskOfResult()
        => ValueTask.FromResult<RemovesAcceptsMetadataResult>(new());

    public Task<RemovesAcceptsMetadataResult> ActionWithNoAcceptsMetadataInTaskOfResult()
        => Task.FromResult<RemovesAcceptsMetadataResult>(new());

    public RemovesAcceptsMetadataActionResult ActionWithNoAcceptsMetadataInActionResult() => new();

    public ValueTask<RemovesAcceptsMetadataActionResult> ActionWithNoAcceptsMetadataInValueTaskOfActionResult()
        => ValueTask.FromResult<RemovesAcceptsMetadataActionResult>(new());

    public Task<RemovesAcceptsMetadataActionResult> ActionWithNoAcceptsMetadataInTaskOfActionResult()
        => Task.FromResult<RemovesAcceptsMetadataActionResult>(new());
}
public class CustomEndpointMetadata
{
    public string Data { get; init; }

    public MetadataSource Source { get; init; }
}

public enum MetadataSource
{
    Caller,
    Parameter,
    ReturnType
}

public class ParameterNameMetadata
{
    public string Name { get; init; }
}

public class AddsCustomParameterMetadata : IEndpointParameterMetadataProvider, IEndpointMetadataProvider
{
    public static void PopulateMetadata(EndpointParameterMetadataContext parameterContext)
    {
        parameterContext.EndpointMetadata?.Add(new ParameterNameMetadata { Name = parameterContext.Parameter?.Name });
    }

    public static void PopulateMetadata(EndpointMetadataContext context)
    {
        context.EndpointMetadata?.Add(new CustomEndpointMetadata { Source = MetadataSource.Parameter });
    }
}

public class AddsCustomEndpointMetadataResult : IEndpointMetadataProvider, IResult
{
    public static void PopulateMetadata(EndpointMetadataContext context)
    {
        context.EndpointMetadata?.Add(new CustomEndpointMetadata { Source = MetadataSource.ReturnType });
    }

    public Task ExecuteAsync(HttpContext httpContext) => Task.Completed.Task;
}

public class AddsCustomEndpointMetadataActionResult : IEndpointMetadataProvider, IActionResult
{
    public static void PopulateMetadata(EndpointMetadataContext context)
    {
        context.EndpointMetadata?.Add(new CustomEndpointMetadata { Source = MetadataSource.ReturnType });
    }
    public Task ExecuteResultAsync(ActionContext context) => Task.Completed.Task;
}

public class RemovesAcceptsMetadataResult : IEndpointMetadataProvider, IResult
{
    public static void PopulateMetadata(EndpointMetadataContext context)
    {
        if (context.EndpointMetadata is not null)
        {
            for (int i = context.EndpointMetadata.Count - 1; i >= 0; i--)
            {
                var metadata = context.EndpointMetadata[i];
                if (metadata is IAcceptsMetadata)
                {
                    context.EndpointMetadata.RemoveAt(i);
                }
            }
        }
    }

    public Task ExecuteAsync(HttpContext httpContext) => Task.Completed.Task;
}

public class RemovesAcceptsMetadataActionResult : IEndpointMetadataProvider, IActionResult
{
    public static void PopulateMetadata(EndpointMetadataContext context)
    {
        if (context.EndpointMetadata is not null)
        {
            for (int i = context.EndpointMetadata.Count - 1; i >= 0; i--)
            {
                var metadata = context.EndpointMetadata[i];
                if (metadata is IAcceptsMetadata)
                {
                    context.EndpointMetadata.RemoveAt(i);
                }
            }
        }
    }

    public Task ExecuteResultAsync(ActionContext context) => Task.Completed.Task;
}

public class RemovesAcceptsParameterMetadata : IEndpointParameterMetadataProvider
{
    public static void PopulateMetadata(EndpointParameterMetadataContext parameterContext)
    {
        if (parameterContext.EndpointMetadata is not null)
        {
            for (int i = parameterContext.EndpointMetadata.Count - 1; i >= 0; i--)
            {
                var metadata = parameterContext.EndpointMetadata[i];
                if (metadata is IAcceptsMetadata)
                {
                    parameterContext.EndpointMetadata.RemoveAt(i);
                }
            }
        }
    }
}

public class RemovesAcceptsParameterEndpointMetadata : IEndpointMetadataProvider
{
    public static void PopulateMetadata(EndpointMetadataContext context)
    {
        if (context.EndpointMetadata is not null)
        {
            for (int i = context.EndpointMetadata.Count - 1; i >= 0; i--)
            {
                var metadata = context.EndpointMetadata[i];
                if (metadata is IAcceptsMetadata)
                {
                    context.EndpointMetadata.RemoveAt(i);
                }
            }
        }
    }
}


