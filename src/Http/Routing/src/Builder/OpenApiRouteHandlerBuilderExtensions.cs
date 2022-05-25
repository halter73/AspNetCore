// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for adding <see cref="Endpoint.Metadata"/> that is
/// meant to be consumed by OpenAPI libraries.
/// </summary>
public static class OpenApiRouteHandlerBuilderExtensions
{
    private static readonly ExcludeFromDescriptionAttribute _excludeFromDescriptionMetadataAttribute = new();

    /// <summary>
    /// Adds the <see cref="IExcludeFromDescriptionMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <returns>A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder ExcludeFromDescription<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(_excludeFromDescriptionMetadataAttribute);

    /// <summary>
    /// Adds the <see cref="IExcludeFromDescriptionMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder ExcludeFromDescription(this RouteHandlerBuilder builder)
        => ExcludeFromDescription<RouteHandlerBuilder>(builder);

    /// <summary>
    /// Adds an <see cref="IProducesResponseTypeMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="responseType">The type of the response. Defaults to null.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="contentType">The response content type. Defaults to "application/json" if responseType is not null, otherwise defaults to null.</param>
    /// <param name="additionalContentTypes">Additional response content types the endpoint produces for the supplied status code.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
#pragma warning disable RS0026
    public static TBuilder Produces<TBuilder>(
#pragma warning restore RS0026
        this TBuilder builder,
        Type? responseType = null,
        int statusCode = StatusCodes.Status200OK,
        string? contentType = null,
        params string[] additionalContentTypes) where TBuilder : IEndpointConventionBuilder
    {
        if (responseType is Type && string.IsNullOrEmpty(contentType))
        {
            contentType = "application/json";
        }

        if (contentType is null)
        {
            return builder.WithMetadata(new ProducesResponseTypeMetadata(responseType ?? typeof(void), statusCode));
        }

        return builder.WithMetadata(new ProducesResponseTypeMetadata(responseType ?? typeof(void), statusCode, contentType, additionalContentTypes));
    }

    /// <summary>
    /// Adds an <see cref="IProducesResponseTypeMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="statusCode">The response status code. Defaults to <see cref="StatusCodes.Status200OK"/>.</param>
    /// <param name="contentType">The response content type. Defaults to "application/json".</param>
    /// <param name="additionalContentTypes">Additional response content types the endpoint produces for the supplied status code.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
#pragma warning disable RS0026
    public static RouteHandlerBuilder Produces<TResponse>(
#pragma warning restore RS0026
        this RouteHandlerBuilder builder,
        int statusCode = StatusCodes.Status200OK,
        string? contentType = null,
        params string[] additionalContentTypes)
    {
        return Produces(builder, typeof(TResponse), statusCode, contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds an <see cref="IProducesResponseTypeMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="responseType">The type of the response. Defaults to null.</param>
    /// <param name="contentType">The response content type. Defaults to "application/json" if responseType is not null, otherwise defaults to null.</param>
    /// <param name="additionalContentTypes">Additional response content types the endpoint produces for the supplied status code.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
#pragma warning disable RS0026
    public static RouteHandlerBuilder Produces(
#pragma warning restore RS0026
        this RouteHandlerBuilder builder,
        int statusCode,
        Type? responseType = null,
        string? contentType = null,
        params string[] additionalContentTypes)
    {
        return Produces(builder, responseType, statusCode, contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds an <see cref="IProducesResponseTypeMetadata"/> with a <see cref="ProblemDetails"/> type
    /// to <see cref="EndpointBuilder.Metadata"/> for all endpoints produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="contentType">The response content type. Defaults to "application/problem+json".</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
#pragma warning disable RS0026
    public static TBuilder ProducesProblem<TBuilder>(
#pragma warning restore RS0026
        this TBuilder builder,
        int statusCode,
        string? contentType = null) where TBuilder : IEndpointConventionBuilder
    {
        if (string.IsNullOrEmpty(contentType))
        {
            contentType = "application/problem+json";
        }

        return Produces(builder, typeof(ProblemDetails), statusCode, contentType);
    }

    /// <summary>
    /// Adds an <see cref="IProducesResponseTypeMetadata"/> with a <see cref="ProblemDetails"/> type
    /// to <see cref="EndpointBuilder.Metadata"/> for all endpoints produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="contentType">The response content type. Defaults to "application/problem+json".</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder ProducesProblem(this RouteHandlerBuilder builder, int statusCode, string? contentType = null)
        => ProducesProblem<RouteHandlerBuilder>(builder, statusCode, contentType);

    /// <summary>
    /// Adds an <see cref="IProducesResponseTypeMetadata"/> with a <see cref="HttpValidationProblemDetails"/> type
    /// to <see cref="EndpointBuilder.Metadata"/> for all endpoints produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="statusCode">The response status code. Defaults to <see cref="StatusCodes.Status400BadRequest"/>.</param>
    /// <param name="contentType">The response content type. Defaults to "application/problem+json".</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
#pragma warning disable RS0026
    public static TBuilder ProducesValidationProblem<TBuilder>(
#pragma warning restore RS0026
        this TBuilder builder,
        int statusCode = StatusCodes.Status400BadRequest,
        string? contentType = null) where TBuilder : IEndpointConventionBuilder
    {
        if (string.IsNullOrEmpty(contentType))
        {
            contentType = "application/problem+json";
        }

        return Produces(builder, typeof(HttpValidationProblemDetails), statusCode, contentType);
    }

    /// <summary>
    /// Adds an <see cref="IProducesResponseTypeMetadata"/> with a <see cref="HttpValidationProblemDetails"/> type
    /// to <see cref="EndpointBuilder.Metadata"/> for all endpoints produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="statusCode">The response status code. Defaults to <see cref="StatusCodes.Status400BadRequest"/>.</param>
    /// <param name="contentType">The response content type. Defaults to "application/problem+json".</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder ProducesValidationProblem(
        this RouteHandlerBuilder builder,
        int statusCode = StatusCodes.Status400BadRequest,
        string? contentType = null)
    {
        return ProducesValidationProblem<RouteHandlerBuilder>(builder, statusCode, contentType);
    }

    /// <summary>
    /// Adds the <see cref="ITagsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    /// The OpenAPI specification supports a tags classification to categorize operations
    /// into related groups. These tags are typically included in the generated specification
    /// and are typically used to group operations by tags in the UI.
    /// </remarks>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="tags">A collection of tags to be associated with the endpoint.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder WithTags<TBuilder>(this TBuilder builder, params string[] tags) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new TagsAttribute(tags));

    /// <summary>
    /// Adds the <see cref="ITagsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    /// The OpenAPI specification supports a tags classification to categorize operations
    /// into related groups. These tags are typically included in the generated specification
    /// and are typically used to group operations by tags in the UI.
    /// </remarks>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="tags">A collection of tags to be associated with the endpoint.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder WithTags(this RouteHandlerBuilder builder, params string[] tags)
        => WithTags<RouteHandlerBuilder>(builder, tags);

    /// <summary>
    /// Adds <see cref="IAcceptsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="requestType">The type of the request body.</param>
    /// <param name="isOptional">Sets a value that determines if the request body is optional.</param>
    /// <param name="contentType">The request content type that the endpoint accepts.</param>
    /// <param name="additionalContentTypes">The list of additional request content types that the endpoint accepts.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder Accepts<TBuilder>(
        this TBuilder builder,
        Type requestType,
        bool isOptional,
        string contentType,
        params string[] additionalContentTypes) where TBuilder : IEndpointConventionBuilder
    {
        var contentTypes = GetAllContentTypes(contentType, additionalContentTypes);
        return builder.WithMetadata(new AcceptsMetadata(requestType, isOptional, contentTypes));
    }

    /// <summary>
    /// Adds <see cref="IAcceptsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="requestType">The type of the request body.</param>
    /// <param name="contentType">The request content type that the endpoint accepts.</param>
    /// <param name="additionalContentTypes">The list of additional request content types that the endpoint accepts.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder Accepts<TBuilder>(
        this TBuilder builder,
        Type requestType,
        string contentType,
        params string[] additionalContentTypes) where TBuilder : IEndpointConventionBuilder
    {
        return Accepts(builder, requestType, isOptional: false, contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds <see cref="IAcceptsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request body.</typeparam>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="contentType">The request content type that the endpoint accepts.</param>
    /// <param name="additionalContentTypes">The list of additional request content types that the endpoint accepts.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder Accepts<TRequest>(
        this RouteHandlerBuilder builder,
        string contentType,
        params string[] additionalContentTypes) where TRequest : notnull
    {
        return Accepts<RouteHandlerBuilder>(builder, typeof(TRequest), contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds <see cref="IAcceptsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request body.</typeparam>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="isOptional">Sets a value that determines if the request body is optional.</param>
    /// <param name="contentType">The request content type that the endpoint accepts.</param>
    /// <param name="additionalContentTypes">The list of additional request content types that the endpoint accepts.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder Accepts<TRequest>(
        this RouteHandlerBuilder builder,
        bool isOptional,
        string contentType,
        params string[] additionalContentTypes) where TRequest : notnull
    {
        return Accepts<RouteHandlerBuilder>(builder, typeof(TRequest), isOptional, contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds <see cref="IAcceptsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="requestType">The type of the request body.</param>
    /// <param name="contentType">The request content type that the endpoint accepts.</param>
    /// <param name="additionalContentTypes">The list of additional request content types that the endpoint accepts.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder Accepts(
        this RouteHandlerBuilder builder,
        Type requestType,
        string contentType,
        params string[] additionalContentTypes)
    {
        return Accepts<RouteHandlerBuilder>(builder, requestType, contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds <see cref="IAcceptsMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="requestType">The type of the request body.</param>
    /// <param name="isOptional">Sets a value that determines if the request body is optional.</param>
    /// <param name="contentType">The request content type that the endpoint accepts.</param>
    /// <param name="additionalContentTypes">The list of additional request content types that the endpoint accepts.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static RouteHandlerBuilder Accepts(
        this RouteHandlerBuilder builder,
        Type requestType,
        bool isOptional,
        string contentType,
        params string[] additionalContentTypes)
    {
        return Accepts<RouteHandlerBuilder>(builder, requestType, isOptional, contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds <see cref="IEndpointDescriptionMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="description">A string representing a detailed description of the endpoint.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder WithDescription<TBuilder>(this TBuilder builder, string description) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new EndpointDescriptionAttribute(description));

    /// <summary>
    /// Adds <see cref="IEndpointSummaryMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all endpoints
    /// produced by <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="summary">A string representing a brief description of the endpoint.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder WithSummary<TBuilder>(this TBuilder builder, string summary) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new EndpointSummaryAttribute(summary));

    private static string[] GetAllContentTypes(string contentType, string[] additionalContentTypes)
    {
        var allContentTypes = new string[additionalContentTypes.Length + 1];
        allContentTypes[0] = contentType;

        for (var i = 0; i < additionalContentTypes.Length; i++)
        {
            allContentTypes[i + 1] = additionalContentTypes[i];
        }

        return allContentTypes;
    }
}
