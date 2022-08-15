// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Http;

internal sealed class RequestDelegateFactoryContext
{
    // Options
    public required IServiceProvider ServiceProvider { get; init; }
    public required IServiceProviderIsService? ServiceProviderIsService { get; init; }
    public required List<string>? RouteParameters { get; init; }
    public required bool ThrowOnBadRequest { get; init; }
    public required bool DisableInferredFromBody { get; init; }

    // The following used to ensure that a cached context on RequestDelegateFactoryOptions.FactoryContext is valid for reuse.
    public required MethodInfo CachedMethodInfo { get; init; }

    // Handler could be null if the MethodInfo overload of RDF.Create is used, but that doesn't matter because this is
    // only referenced to optimize certain cases where a RequestDelegate is the handler and filters don't modify it.
    public Delegate? Handler { get; set; }

    // Temporary State
    public ParameterInfo? JsonRequestBodyParameter { get; set; }
    public bool AllowEmptyRequestBody { get; set; }

    public bool UsingTempSourceString { get; set; }
    public List<ParameterExpression> ExtraLocals { get; } = new();
    public List<Expression> ParamCheckExpressions { get; } = new();
    public List<Func<HttpContext, ValueTask<object?>>> ParameterBinders { get; } = new();

    public Dictionary<string, string> TrackedParameters { get; } = new();
    public bool HasMultipleBodyParameters { get; set; }
    public bool HasInferredBody { get; set; }

    public required EndpointBuilder EndpointBuilder { get; init; }

    public NullabilityInfoContext NullabilityContext { get; } = new();

    public bool ReadForm { get; set; }
    public ParameterInfo? FirstFormRequestBodyParameter { get; set; }
    // Properties for constructing and managing filters
    public List<Expression> ContextArgAccess { get; } = new();
    public Expression? MethodCall { get; set; }
    public Type[] ArgumentTypes { get; set; } = Array.Empty<Type>();
    public Expression[]? ArgumentExpressions { get; set; }
    public Expression[] BoxedArgs { get; set; } = Array.Empty<Expression>();
    public bool FilterFactoriesHaveRunWithoutModifyingPerRequestBehavior { get; set; }

    public List<ParameterInfo> Parameters { get; set; } = new();
}
