// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace MvcSandbox.Controllers;

public class MetadataProviderController
{
    public ActionResult ActionWithParameterMetadata(AddsCustomParameterMetadata param1) => null;
    public ActionResult ActionWithRemovalFromParameterMetadata(RemovesAcceptsParameterMetadata param1) => null;
    public ActionResult ActionWithRemovalFromParameterEndpointMetadata(RemovesAcceptsParameterEndpointMetadata param1) => null;

    [HttpGet("selector1")]
    [HttpGet("selector2")]
    public ActionResult MultipleSelectorsActionWithParameterMetadata(AddsCustomParameterMetadata param1) => null;

    public AddsCustomEndpointMetadataResult ActionWithMetadataInResult() => null;

    public ValueTask<AddsCustomEndpointMetadataResult> ActionWithMetadataInValueTaskOfResult()
        => ValueTask.FromResult<AddsCustomEndpointMetadataResult>(null);

    public Task<AddsCustomEndpointMetadataResult> ActionWithMetadataInTaskOfResult()
        => Task.FromResult<AddsCustomEndpointMetadataResult>(null);

    [HttpGet("selector1")]
    [HttpGet("selector2")]
    public AddsCustomEndpointMetadataActionResult MultipleSelectorsActionWithMetadataInActionResult() => null;

    public AddsCustomEndpointMetadataActionResult ActionWithMetadataInActionResult() => null;

    public ValueTask<AddsCustomEndpointMetadataActionResult> ActionWithMetadataInValueTaskOfActionResult()
        => ValueTask.FromResult<AddsCustomEndpointMetadataActionResult>(null);

    public Task<AddsCustomEndpointMetadataActionResult> ActionWithMetadataInTaskOfActionResult()
        => Task.FromResult<AddsCustomEndpointMetadataActionResult>(null);

    public RemovesAcceptsMetadataResult ActionWithNoAcceptsMetadataInResult() => null;

    public ValueTask<RemovesAcceptsMetadataResult> ActionWithNoAcceptsMetadataInValueTaskOfResult()
        => ValueTask.FromResult<RemovesAcceptsMetadataResult>(null);

    public Task<RemovesAcceptsMetadataResult> ActionWithNoAcceptsMetadataInTaskOfResult()
        => Task.FromResult<RemovesAcceptsMetadataResult>(null);

    public RemovesAcceptsMetadataActionResult ActionWithNoAcceptsMetadataInActionResult() => null;

    public ValueTask<RemovesAcceptsMetadataActionResult> ActionWithNoAcceptsMetadataInValueTaskOfActionResult()
        => ValueTask.FromResult<RemovesAcceptsMetadataActionResult>(null);

    public Task<RemovesAcceptsMetadataActionResult> ActionWithNoAcceptsMetadataInTaskOfActionResult()
        => Task.FromResult<RemovesAcceptsMetadataActionResult>(null);
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

    public Task ExecuteAsync(HttpContext httpContext) => throw new NotImplementedException();
}

public class AddsCustomEndpointMetadataActionResult : IEndpointMetadataProvider, IActionResult
{
    public static void PopulateMetadata(EndpointMetadataContext context)
    {
        context.EndpointMetadata?.Add(new CustomEndpointMetadata { Source = MetadataSource.ReturnType });
    }
    public Task ExecuteResultAsync(ActionContext context) => throw new NotImplementedException();
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

    public Task ExecuteAsync(HttpContext httpContext) => throw new NotImplementedException();
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

    public Task ExecuteResultAsync(ActionContext context) => throw new NotImplementedException();
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


