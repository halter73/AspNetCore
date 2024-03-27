using Microsoft.OpenApi.Models;

namespace Microsoft.AspNetCore.OpenApi;

internal sealed class DelegateOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly Func<OpenApiDocument, OpenApiDocumentTransformerContext, CancellationToken, Task>? _documentTransformer;
    private readonly Func<OpenApiOperation, OpenApiOperationTransformerContext, CancellationToken, Task>? _operationTransformer;
    private readonly Func<OpenApiSchema, OpenApiSchemaTransformerContext, CancellationToken, Task>? _schemaTransformer;

    public DelegateOpenApiDocumentTransformer(Func<OpenApiDocument, OpenApiDocumentTransformerContext, CancellationToken, Task> transformer)
    {
        _documentTransformer = transformer;
    }

    public DelegateOpenApiDocumentTransformer(Func<OpenApiOperation, OpenApiOperationTransformerContext, CancellationToken, Task> transformer)
    {
        _operationTransformer = transformer;
    }

    public DelegateOpenApiDocumentTransformer(Func<OpenApiSchema, OpenApiSchemaTransformerContext, CancellationToken, Task> transformer)
    {
        _schemaTransformer = transformer;
    }

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (_documentTransformer != null)
        {
            return _documentTransformer(document, context, cancellationToken);
        }

        if (_operationTransformer != null)
        {
            foreach (var pathItem in document.Paths.Values)
            {
                foreach (var operation in pathItem.Operations.Values)
                {
                    var operationContext = new OpenApiOperationTransformerContext
                    {
                        DocumentName = context.DocumentName,
                        Description = new Mvc.ApiExplorer.ApiDescription(),
                        ApplicationServices = context.ApplicationServices,
                    };
                    return _operationTransformer(operation, operationContext, cancellationToken);
                }
            }
        }

        if (_schemaTransformer != null)
        {
            foreach (var schema in document.Components.Schemas.Values)
            {
                var schemaContext = new OpenApiSchemaTransformerContext
                {
                    Type = schema.GetType(),
                    DocumentName = context.DocumentName,
                    ApplicationServices = context.ApplicationServices,
                };
                return _schemaTransformer(schema, schemaContext, cancellationToken);
            }
        }

        return Task.CompletedTask;
    }
}
