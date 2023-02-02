// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel;

internal static class StaticRouteHandlerModelEmitter
{
    public static string EmitHandlerDelegateType(this Endpoint endpoint)
    {
        if (endpoint.Parameters.Length == 0)
        {
            return endpoint.Response.IsVoid ? "System.Action" : $"System.Func<{endpoint.Response.WrappedResponseType}>";
        }
        else
        {
            var parameterTypeList = string.Join(", ", endpoint.Parameters.Select(p => p.Type));

            if (endpoint.Response.IsVoid)
            {
                return $"System.Action<{parameterTypeList}>";
            }
            else
            {
                return $"System.Func<{parameterTypeList}, {endpoint.Response.WrappedResponseType}>";
            }
        }
    }

    public static string EmitSourceKey(this Endpoint endpoint)
    {
        return $@"(@""{endpoint.Location.Item1}"", {endpoint.Location.Item2})";
    }

    public static string EmitVerb(this Endpoint endpoint)
    {
        return endpoint.HttpMethod switch
        {
            "MapGet" => "GetVerb",
            "MapPut" => "PutVerb",
            "MapPost" => "PostVerb",
            "MapDelete" => "DeleteVerb",
            "MapPatch" => "PatchVerb",
            _ => throw new ArgumentException($"Received unexpected HTTP method: {endpoint.HttpMethod}")
        };
    }

    /*
     * TODO: Emit invocation to the request handler. The structure
     * involved here consists of a call to bind parameters, check
     * their validity (optionality), invoke the underlying handler with
     * the arguments bound from HTTP context, and write out the response.
     */
    public static string EmitRequestHandler(this Endpoint endpoint)
    {
        var code = new CodeWriter(new StringBuilder());
        code.Indent(5);
        code.WriteLine(endpoint.Response.IsAwaitable
            ? "async Task RequestHandler(HttpContext httpContext)"
            : "Task RequestHandler(HttpContext httpContext)");
        code.StartBlock();

        if (endpoint.Response.IsVoid)
        {
            code.WriteLine($"handler({endpoint.EmitArgumentList()});");
            code.WriteLine("return Task.CompletedTask;");
        }
        else
        {
            code.WriteLine($"""httpContext.Response.ContentType ??= "{endpoint.Response.ContentType}";""");
            if (endpoint.Response.IsAwaitable)
            {
                code.WriteLine($"var result = await handler({endpoint.EmitArgumentList()});");
                code.WriteLine(endpoint.EmitResponseWritingCall());
            }
            else
            {
                code.WriteLine($"var result = handler({endpoint.EmitArgumentList()});");
                code.WriteLine("return GeneratedRouteBuilderExtensionsCore.ExecuteObjectResult(result, httpContext);");
            }
        }
        code.EndBlock();
        return code.ToString();
    }

    private static string EmitResponseWritingCall(this Endpoint endpoint)
    {
        var code = new CodeWriter(new StringBuilder());
        code.WriteNoIndent(endpoint.Response.IsAwaitable ? "await " : "return ");

        if (endpoint.Response.IsIResult)
        {
            code.WriteNoIndent("result.ExecuteAsync(httpContext);");
        }
        else if (endpoint.Response.ResponseType.SpecialType == SpecialType.System_String)
        {
            code.WriteNoIndent("httpContext.Response.WriteAsync(result);");
        }
        else if (endpoint.Response.ResponseType.SpecialType == SpecialType.System_Object)
        {
            code.WriteNoIndent("GeneratedRouteBuilderExtensionsCore.ExecuteObjectResult(result, httpContext);");
        }
        else if (!endpoint.Response.IsVoid)
        {
            code.WriteNoIndent("httpContext.Response.WriteAsJsonAsync(result);");
        }
        else if (!endpoint.Response.IsAwaitable && endpoint.Response.IsVoid)
        {
            code.WriteNoIndent("Task.CompletedTask;");
        }

        return code.ToString();
    }

    public static string EmitFilteredRequestHandler(this Endpoint endpoint)
    {
        var code = new CodeWriter(new StringBuilder());
        code.Indent(5);
        code.WriteLine("async Task RequestHandlerFiltered(HttpContext httpContext)");
        code.StartBlock();
        if (endpoint.Parameters.Length == 0)
        {
            code.WriteLine("var result = await filteredInvocation(new DefaultEndpointFilterInvocationContext(httpContext));");
        }
        else
        {
            code.WriteLine($"var result = await filteredInvocation(new DefaultEndpointFilterInvocationContext(httpContext, {endpoint.EmitArgumentList()}));");
        }
        code.WriteLine("await GeneratedRouteBuilderExtensionsCore.ExecuteObjectResult(result, httpContext);");
        code.EndBlock();
        return code.ToString();
    }

    public static string EmitFilteredInvocation(this Endpoint endpoint)
    {
        var code = new CodeWriter(new StringBuilder());
        code.Indent(7);
        
        if (endpoint.Response.IsVoid)
        {
            code.WriteLine($"handler({endpoint.EmitFilteredArgumentList()});");
            code.WriteLine("return ValueTask.FromResult<object?>(Results.Empty);");
        }
        else
        {
            code.WriteLine($"return ValueTask.FromResult<object?>(handler({endpoint.EmitFilteredArgumentList()}));");
        }

        return code.ToString();
    }

    public static string EmitFilteredArgumentList(this Endpoint endpoint)
    {
        if (endpoint.Parameters.Length == 0)
        {
            return "";
        }

        var sb = new StringBuilder();

        for (var i = 0; i < endpoint.Parameters.Length; i++)
        {
            sb.Append($"ic.GetArgument<{endpoint.Parameters[i].Type}>({i})");

            if (i < endpoint.Parameters.Length - 1)
            {
                sb.Append(", ");
            }
        }

        return sb.ToString();
    }
}
