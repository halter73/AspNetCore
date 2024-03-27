using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Represents the context in which an OpenAPI schema transformer is executed.
/// </summary>
public sealed class OpenApiSchemaTransformerContext
{
    /// <summary>
    /// Gets the name of the associated OpenAPI document.
    /// </summary>
    public required string DocumentName { get; init; }

    /// <summary>
    /// Gets the <see cref="Type" /> associated with the current schema.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// Gets the (optional) <see cref="ApiParameterDescription"/> associated with the current schema.
    /// </summary>
    public ApiParameterDescription? ParameterDescription { get; init; }

    /// <summary>
    /// Gets the application services associated with current document.
    /// </summary>
    public required IServiceProvider ApplicationServices { get; init; }
}
