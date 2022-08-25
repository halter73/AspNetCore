// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace MvcSandbox.Controllers;

[ApiController]
public class HomeController : Controller
{
    [ModelBinder]
    public string Id { get; set; }

    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("selector1")]
    public ActionResult ActionWithParameterMetadata(AddsCustomParameterMetadata param1) => View();

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
        public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder)
        {
            builder.Metadata.Add(new ParameterNameMetadata { Name = parameter.Name });
        }

        public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        {
            builder.Metadata.Add(new CustomEndpointMetadata { Source = MetadataSource.Parameter });
        }
    }

    public class AddsCustomEndpointMetadataResult : IEndpointMetadataProvider, IResult
    {
        public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        {
            builder.Metadata.Add(new CustomEndpointMetadata { Source = MetadataSource.ReturnType });
        }

        public Task ExecuteAsync(HttpContext httpContext) => throw new NotImplementedException();
    }

    public class AddsCustomEndpointMetadataActionResult : IEndpointMetadataProvider, IActionResult
    {
        public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        {
            builder.Metadata.Add(new CustomEndpointMetadata { Source = MetadataSource.ReturnType });
        }
        public Task ExecuteResultAsync(ActionContext context) => throw new NotImplementedException();
    }

    public class RemovesAcceptsMetadataResult : IEndpointMetadataProvider, IResult
    {
        public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        {
            for (int i = builder.Metadata.Count - 1; i >= 0; i--)
            {
                var metadata = builder.Metadata[i];
                if (metadata is IAcceptsMetadata)
                {
                    builder.Metadata.RemoveAt(i);
                }
            }
        }

        public Task ExecuteAsync(HttpContext httpContext) => throw new NotImplementedException();
    }

    public class RemovesAcceptsMetadataActionResult : IEndpointMetadataProvider, IActionResult
    {
        public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        {
            if (builder.Metadata is not null)
            {
                for (int i = builder.Metadata.Count - 1; i >= 0; i--)
                {
                    var metadata = builder.Metadata[i];
                    if (metadata is IAcceptsMetadata)
                    {
                        builder.Metadata.RemoveAt(i);
                    }
                }
            }
        }

        public Task ExecuteResultAsync(ActionContext context) => throw new NotImplementedException();
    }

    public class RemovesAcceptsParameterMetadata : IEndpointParameterMetadataProvider
    {
        public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder)
        {
            if (builder.Metadata is not null)
            {
                for (int i = builder.Metadata.Count - 1; i >= 0; i--)
                {
                    var metadata = builder.Metadata[i];
                    if (metadata is IAcceptsMetadata)
                    {
                        builder.Metadata.RemoveAt(i);
                    }
                }
            }
        }
    }

    public class RemovesAcceptsParameterEndpointMetadata : IEndpointMetadataProvider
    {
        public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        {
            if (builder.Metadata is not null)
            {
                for (int i = builder.Metadata.Count - 1; i >= 0; i--)
                {
                    var metadata = builder.Metadata[i];
                    if (metadata is IAcceptsMetadata)
                    {
                        builder.Metadata.RemoveAt(i);
                    }
                }
            }
        }
    }
}
