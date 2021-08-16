// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// Extension methods for adding response type metadata to endpoints.
    /// </summary>
    public static class OpenApiEndpointConventionBuilderExtensions
    {
        private static readonly ExcludeFromDescriptionAttribute _excludeFromDescriptionMetadataAttribute = new();

        /// <summary>
        /// Adds the <see cref="IExcludeFromDescriptionMetadata"/> to <see cref="EndpointBuilder.Metadata"/> for all builders
        /// produced by <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The endpoint convention builder.</param>
        /// <returns>The original convention builder parameter.</returns>
        public static TBuilder ExcludeFromDescription<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        {
            builder.WithMetadata(_excludeFromDescriptionMetadataAttribute);

            return builder;
        }

        /// <summary>
        /// Adds the <see cref="ProducesResponseTypeAttribute"/> to <see cref="EndpointBuilder.Metadata"/> for all builders
        /// produced by <paramref name="builder"/>.
        /// </summary>
        /// <typeparam name="TBuilder">The concrete type of the <see cref="IEndpointConventionBuilder"/>.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="builder">The endpoint convention builder.</param>
        /// <param name="statusCode">The response status code. Defaults to StatusCodes.Status200OK.</param>
        /// <param name="contentType">The response content type. Defaults to "application/json".</param>
        /// <param name="additionalContentTypes">Additional response content types the endpoint produces for the supplied status code.</param>
        /// <returns>The original convention builder parameter.</returns>
#pragma warning disable RS0026
        public static TBuilder Produces<TBuilder, TResponse>(this TBuilder builder,
#pragma warning restore RS0026
            int statusCode = StatusCodes.Status200OK,
            string? contentType =  null,
            params string[] additionalContentTypes) where TBuilder : IEndpointConventionBuilder
        {
            return Produces(builder, statusCode, typeof(TResponse), contentType, additionalContentTypes);
        }

        /// <summary>
        /// Adds the <see cref="ProducesResponseTypeAttribute"/> to <see cref="EndpointBuilder.Metadata"/> for all builders
        /// produced by <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The endpoint convention builder.</param>
        /// <param name="statusCode">The response status code.</param>
        /// <param name="responseType">The type of the response. Defaults to null.</param>
        /// <param name="contentType">The response content type. Defaults to "application/json" if responseType is not null, otherwise defaults to null.</param>
        /// <param name="additionalContentTypes">Additional response content types the endpoint produces for the supplied status code.</param>
        /// <returns>The original convention builder parameter.</returns>
#pragma warning disable RS0026
        public static TBuilder Produces<TBuilder>(this TBuilder builder,
#pragma warning restore RS0026
            int statusCode,
            Type? responseType = null,
            string? contentType = null,
            params string[] additionalContentTypes) where TBuilder : IEndpointConventionBuilder
        {
            if (responseType is Type && string.IsNullOrEmpty(contentType))
            {
                contentType = "application/json";
            }

            if (contentType is null)
            {
                builder.WithMetadata(new ProducesResponseTypeAttribute(responseType ?? typeof(void), statusCode));
                return builder;
            }

            builder.WithMetadata(new ProducesResponseTypeAttribute(responseType ?? typeof(void), statusCode, contentType, additionalContentTypes));

            return builder;
        }

        /// <summary>
        /// Adds the <see cref="ProducesResponseTypeAttribute"/> with a <see cref="ProblemDetails"/> type
        /// to <see cref="EndpointBuilder.Metadata"/> for all builders produced by <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The endpoint convention builder.</param>
        /// <param name="statusCode">The response status code.</param>
        /// <param name="contentType">The response content type. Defaults to "application/problem+json".</param>
        /// <returns>The original convention builder parameter.</returns>
        public static TBuilder ProducesProblem<TBuilder>(this TBuilder builder,
            int statusCode,
            string? contentType = null) where TBuilder : IEndpointConventionBuilder
        {
            if (string.IsNullOrEmpty(contentType))
            {
                contentType = "application/problem+json";
            }

            return Produces<TBuilder, ProblemDetails>(builder, statusCode, contentType);
        }

        /// <summary>
        /// Adds the <see cref="ProducesResponseTypeAttribute"/> with a <see cref="HttpValidationProblemDetails"/> type
        /// to <see cref="EndpointBuilder.Metadata"/> for all builders produced by <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The endpoint convention builder.</param>
        /// <param name="statusCode">The response status code. Defaults to StatusCodes.Status400BadRequest.</param>
        /// <param name="contentType">The response content type. Defaults to "application/validationproblem+json".</param>
        /// <returns>The original convention builder parameter.</returns>
        public static TBuilder ProducesValidationProblem<TBuilder>(this TBuilder builder,
            int statusCode = StatusCodes.Status400BadRequest,
            string? contentType = null) where TBuilder : IEndpointConventionBuilder
        {
            if (string.IsNullOrEmpty(contentType))
            {
                contentType = "application/validationproblem+json";
            }

            return Produces<TBuilder, HttpValidationProblemDetails>(builder, statusCode, contentType);
        }
    }
}
