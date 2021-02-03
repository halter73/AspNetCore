// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Api.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Http.Api
{
    // Borrows heavily from https://github.com/davidfowl/uController/blob/aa8bcb4b30764e42bd72d326cb2718a4d4eaf4a9/src/uController/HttpHandlerBuilder.cs
    internal class MapActionExpressionTreeBuilder
    {
        private static readonly MethodInfo ChangeTypeMethodInfo = GetMethodInfo<Func<object, Type, object>>((value, type) => Convert.ChangeType(value, type));
        private static readonly MethodInfo ExecuteTaskOfTMethodInfo = typeof(MapActionExpressionTreeBuilder).GetMethod(nameof(ExecuteTask), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo ExecuteValueTaskOfTMethodInfo = typeof(MapActionExpressionTreeBuilder).GetMethod(nameof(ExecuteValueTask), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo ExecuteTaskResultOfTMethodInfo = typeof(MapActionExpressionTreeBuilder).GetMethod(nameof(ExecuteTaskResult), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo ExecuteValueResultTaskOfTMethodInfo = typeof(MapActionExpressionTreeBuilder).GetMethod(nameof(ExecuteValueTaskResult), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo GetRequiredServiceMethodInfo = typeof(ServiceProviderServiceExtensions).GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService), BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(IServiceProvider) })!;
        private static readonly MethodInfo ResultWriteResponseAsync = typeof(IResult).GetMethod(nameof(IResult.WriteResponseAsync), BindingFlags.Public | BindingFlags.Instance)!;
        private static readonly MethodInfo StringResultWriteResponseAsync = GetMethodInfo<Func<HttpResponse, string, Task>>((response, text) => HttpResponseWritingExtensions.WriteAsync(response, text, default));
        private static readonly MethodInfo JsonResultWriteResponseAsync = GetMethodInfo<Func<HttpResponse, object, Task>>((response, value) => HttpResponseJsonExtensions.WriteAsJsonAsync(response, value, default));

        private static readonly ParameterExpression HttpContextParameter = Expression.Parameter(typeof(HttpContext), "httpContext");

        private static readonly MethodInfo ReadFromJsonAsyncMethodInfo =
            typeof(HttpRequestJsonExtensions).GetMethod(nameof(HttpRequestJsonExtensions.ReadFromJsonAsync),
                BindingFlags.Public, new Type[] { typeof(HttpRequest), typeof(CancellationToken) })!;

        private static Expression<Func<HttpContext, ValueTask>> BuildExpression(Delegate action)
        {
            var methodInfo = action.GetMethodInfo();
            var parameters = methodInfo.GetParameters();
            var arguments = new Expression[parameters.Length];

            var boundBody = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                foreach (var attribute in parameter.CustomAttributes)
                {
                    if (attribute.AttributeType.IsAssignableTo(typeof(IFromBodyAttribute)))
                    {
                        if (boundBody)
                        {
                            throw new InvalidOperationException("Action cannot have more than one FromBody attribute.");
                        }

                        arguments[i] = BuildJsonFromBodyArgument(parameter.ParameterType);
                        boundBody = true;
                        break;
                    }
                }

                if (arguments[i] is null)
                {
                    throw new InvalidOperationException($"Could not bind parameter {i}: {parameter}");
                }
            }

        }

        private static Expression BuildJsonFromBodyArgument(Type parameterType)
        {
            var request = Expression.Property(HttpContextParameter, nameof(HttpContext.Request));
            var readMethod = ReadFromJsonAsyncMethodInfo.MakeGenericMethod(parameterType);
            return Expression.Call(readMethod, request, Expression.Constant(CancellationToken.None));
        }

        private static Expression BindArgument(Expression sourceExpression, ParameterInfo parameter, string name)
        {
            var key = name ?? parameter.Name;
            var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            var valueArg = Expression.Convert(
                                Expression.MakeIndex(sourceExpression,
                                                     sourceExpression.Type.GetProperty("Item"),
                                                     new[] {
                                                         Expression.Constant(key)
                                                     }),
                                typeof(string));

            MethodInfo parseMethod = (from m in type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                      let parameters = m.GetParameters()
                                      where m.Name == "Parse" && parameters.Length == 1 && parameters[0].ParameterType == typeof(string)
                                      select m).FirstOrDefault()!;

            Expression? expr = null;

            if (parseMethod != null)
            {
                expr = Expression.Call(parseMethod, valueArg);
            }
            else if (parameter.ParameterType != valueArg.Type)
            {
                // Convert.ChangeType()
                expr = Expression.Call(ChangeTypeMethodInfo, valueArg, Expression.Constant(type));
            }
            else
            {
                expr = valueArg;
            }

            if (expr.Type != parameter.ParameterType)
            {
                expr = Expression.Convert(expr, parameter.ParameterType);
            }

            // property[key] == null ? default : (ParameterType){Type}.Parse(property[key]);
            expr = Expression.Condition(
                Expression.Equal(valueArg, Expression.Constant(null)),
                Expression.Default(parameter.ParameterType),
                expr);

            return expr;
        }

        private static MethodInfo GetMethodInfo<T>(Expression<T> expr)
        {
            var mc = (MethodCallExpression)expr.Body;
            return mc.Method;
        }

        private static MemberInfo GetMemberInfo<T>(Expression<T> expr)
        {
            var mc = (MemberExpression)expr.Body;
            return mc.Member;
        }

        private static async ValueTask ExecuteTask<T>(Task<T> task, HttpContext httpContext)
        {
            await new JsonResult(await task).WriteResponseAsync(httpContext);
        }

        private static ValueTask ExecuteValueTask<T>(ValueTask<T> task, HttpContext httpContext)
        {
            static async ValueTask ExecuteAwaited(ValueTask<T> task, HttpContext httpContext)
            {
                await new JsonResult(await task).WriteResponseAsync(httpContext);
            }

            if (task.IsCompletedSuccessfully)
            {
                return new JsonResult(task.GetAwaiter().GetResult()).WriteResponseAsync(httpContext);
            }

            return ExecuteAwaited(task, httpContext);
        }

        private static ValueTask ExecuteValueTaskResult<T>(ValueTask<T> task, HttpContext httpContext) where T : IResult
        {
            static async ValueTask ExecuteAwaited(ValueTask<T> task, HttpContext httpContext)
            {
                await (await task).WriteResponseAsync(httpContext);
            }

            if (task.IsCompletedSuccessfully)
            {
                return task.GetAwaiter().GetResult().WriteResponseAsync(httpContext);
            }

            return ExecuteAwaited(task, httpContext);
        }

        private static async Task ExecuteTaskResult<T>(Task<T> task, HttpContext httpContext) where T : IResult
        {
            await (await task).WriteResponseAsync(httpContext);
        }

        /// <summary>
        /// Equivalent to the IResult part of Microsoft.AspNetCore.Mvc.JsonResult
        /// </summary>
        private class JsonResult : IResult
        {
            public object? Value { get; }

            public JsonResult(object? value)
            {
                Value = value;
            }

            public ValueTask WriteResponseAsync(HttpContext httpContext)
            {
                return new ValueTask(httpContext.Response.WriteAsJsonAsync(Value));
            }
        }
    }
}
