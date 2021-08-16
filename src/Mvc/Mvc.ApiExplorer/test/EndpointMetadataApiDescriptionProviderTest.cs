// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.ApiExplorer
{
    public class EndpointMetadataApiDescriptionProviderTest
    {
        [Fact]
        public void MultipleApiDescriptionsCreatedForMultipleHttpMethods()
        {
            var apiDescriptions = GetApiDescriptions(() => { }, "/", new string[] { "FOO", "BAR" });

            Assert.Equal(2, apiDescriptions.Count);
        }

        [Fact]
        public void ApiDescriptionNotCreatedIfNoHttpMethods()
        {
            var apiDescriptions = GetApiDescriptions(() => { }, "/", Array.Empty<string>());

            Assert.Empty(apiDescriptions);
        }

        [Fact]
        public void UsesDeclaringTypeAsControllerName()
        {
            var apiDescription = GetApiDescription(TestAction);

            var declaringTypeName = typeof(EndpointMetadataApiDescriptionProviderTest).Name;
            Assert.Equal(declaringTypeName, apiDescription.ActionDescriptor.RouteValues["controller"]);
        }

        [Fact]
        public void UsesApplicationNameAsControllerNameIfNoDeclaringType()
        {
            var apiDescription = GetApiDescription(() => { });

            Assert.Equal(nameof(EndpointMetadataApiDescriptionProviderTest), apiDescription.ActionDescriptor.RouteValues["controller"]);
        }

        [Fact]
        public void AddsJsonRequestFormatWhenFromBodyInferred()
        {
            static void AssertJsonRequestFormat(ApiDescription apiDescription)
            {
                var requestFormat = Assert.Single(apiDescription.SupportedRequestFormats);
                Assert.Equal("application/json", requestFormat.MediaType);
                Assert.Null(requestFormat.Formatter);
            }

            AssertJsonRequestFormat(GetApiDescription(
                (Todo fromBody) => { }));

            AssertJsonRequestFormat(GetApiDescription(
                ([FromBody] int fromBody) => { }));
        }

        [Fact]
        public void AddsRequestFormatFromMetadata()
        {
            static void AssertCustomRequestFormat(ApiDescription apiDescription)
            {
                var requestFormat = Assert.Single(apiDescription.SupportedRequestFormats);
                Assert.Equal("application/custom", requestFormat.MediaType);
                Assert.Null(requestFormat.Formatter);
            }

            AssertCustomRequestFormat(GetApiDescription(
                [Consumes("application/custom")]
            (Todo fromBody) =>
                { }));

            AssertCustomRequestFormat(GetApiDescription(
                [Consumes("application/custom")]
            ([FromBody] int fromBody) =>
                { }));
        }

        [Fact]
        public void AddsMultipleRequestFormatsFromMetadata()
        {
            var apiDescription = GetApiDescription(
                [Consumes("application/custom0", "application/custom1")]
            (Todo fromBody) =>
                { });

            Assert.Equal(2, apiDescription.SupportedRequestFormats.Count);

            var requestFormat0 = apiDescription.SupportedRequestFormats[0];
            Assert.Equal("application/custom0", requestFormat0.MediaType);
            Assert.Null(requestFormat0.Formatter);

            var requestFormat1 = apiDescription.SupportedRequestFormats[1];
            Assert.Equal("application/custom1", requestFormat1.MediaType);
            Assert.Null(requestFormat1.Formatter);
        }

        [Fact]
        public void AddsJsonResponseFormatWhenFromBodyInferred()
        {
            static void AssertJsonResponse(ApiDescription apiDescription, Type expectedType)
            {
                var responseType = Assert.Single(apiDescription.SupportedResponseTypes);
                Assert.Equal(200, responseType.StatusCode);
                Assert.Equal(expectedType, responseType.Type);
                Assert.Equal(expectedType, responseType.ModelMetadata.ModelType);

                var responseFormat = Assert.Single(responseType.ApiResponseFormats);
                Assert.Equal("application/json", responseFormat.MediaType);
                Assert.Null(responseFormat.Formatter);
            }

            AssertJsonResponse(GetApiDescription(() => new Todo()), typeof(Todo));
            AssertJsonResponse(GetApiDescription(() => (IInferredJsonInterface)null), typeof(IInferredJsonInterface));
        }

        [Fact]
        public void AddsTextResponseFormatWhenFromBodyInferred()
        {
            var apiDescription = GetApiDescription(() => "foo");

            var responseType = Assert.Single(apiDescription.SupportedResponseTypes);
            Assert.Equal(200, responseType.StatusCode);
            Assert.Equal(typeof(string), responseType.Type);
            Assert.Equal(typeof(string), responseType.ModelMetadata.ModelType);

            var responseFormat = Assert.Single(responseType.ApiResponseFormats);
            Assert.Equal("text/plain", responseFormat.MediaType);
            Assert.Null(responseFormat.Formatter);
        }

        [Fact]
        public void AddsNoResponseFormatWhenItCannotBeInferredAndTheresNoMetadata()
        {
            static void AssertVoid(ApiDescription apiDescription)
            {
                var responseType = Assert.Single(apiDescription.SupportedResponseTypes);
                Assert.Equal(200, responseType.StatusCode);
                Assert.Equal(typeof(void), responseType.Type);
                Assert.Equal(typeof(void), responseType.ModelMetadata.ModelType);

                Assert.Empty(responseType.ApiResponseFormats);
            }

            AssertVoid(GetApiDescription(() => { }));
            AssertVoid(GetApiDescription(() => Task.CompletedTask));
            AssertVoid(GetApiDescription(() => new ValueTask()));
        }

        [Fact]
        public void AddsResponseFormatFromMetadata()
        {
            var apiDescription = GetApiDescription(
                [ProducesResponseType(typeof(TimeSpan), StatusCodes.Status201Created)]
            [Produces("application/custom")]
            () => new Todo());

            var responseType = Assert.Single(apiDescription.SupportedResponseTypes);

            Assert.Equal(201, responseType.StatusCode);
            Assert.Equal(typeof(TimeSpan), responseType.Type);
            Assert.Equal(typeof(TimeSpan), responseType.ModelMetadata.ModelType);

            var responseFormat = Assert.Single(responseType.ApiResponseFormats);
            Assert.Equal("application/custom", responseFormat.MediaType);
        }

        [Fact]
        public void AddsMultipleResponseFormatsFromMetadataWithPoco()
        {
            var apiDescription = GetApiDescription(
                [ProducesResponseType(typeof(TimeSpan), StatusCodes.Status201Created)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            () => new Todo());

            Assert.Equal(2, apiDescription.SupportedResponseTypes.Count);

            var createdResponseType = apiDescription.SupportedResponseTypes[0];

            Assert.Equal(201, createdResponseType.StatusCode);
            Assert.Equal(typeof(TimeSpan), createdResponseType.Type);
            Assert.Equal(typeof(TimeSpan), createdResponseType.ModelMetadata.ModelType);

            var createdResponseFormat = Assert.Single(createdResponseType.ApiResponseFormats);
            Assert.Equal("application/json", createdResponseFormat.MediaType);

            var badRequestResponseType = apiDescription.SupportedResponseTypes[1];

            Assert.Equal(400, badRequestResponseType.StatusCode);
            Assert.Equal(typeof(Todo), badRequestResponseType.Type);
            Assert.Equal(typeof(Todo), badRequestResponseType.ModelMetadata.ModelType);

            var badRequestResponseFormat = Assert.Single(badRequestResponseType.ApiResponseFormats);
            Assert.Equal("application/json", badRequestResponseFormat.MediaType);
        }

        [Fact]
        public void AddsMultipleResponseFormatsFromMetadataWithIResult()
        {
            var apiDescription = GetApiDescription(
                [ProducesResponseType(typeof(Todo), StatusCodes.Status201Created)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            () => Results.Ok(new Todo()));

            Assert.Equal(2, apiDescription.SupportedResponseTypes.Count);

            var createdResponseType = apiDescription.SupportedResponseTypes[0];

            Assert.Equal(201, createdResponseType.StatusCode);
            Assert.Equal(typeof(Todo), createdResponseType.Type);
            Assert.Equal(typeof(Todo), createdResponseType.ModelMetadata.ModelType);

            var createdResponseFormat = Assert.Single(createdResponseType.ApiResponseFormats);
            Assert.Equal("application/json", createdResponseFormat.MediaType);

            var badRequestResponseType = apiDescription.SupportedResponseTypes[1];

            Assert.Equal(400, badRequestResponseType.StatusCode);
            Assert.Equal(typeof(void), badRequestResponseType.Type);
            Assert.Equal(typeof(void), badRequestResponseType.ModelMetadata.ModelType);

            Assert.Empty(badRequestResponseType.ApiResponseFormats);
        }

        [Fact]
        public void AddsFromRouteParameterAsPath()
        {
            static void AssertPathParameter(ApiDescription apiDescription)
            {
                var param = Assert.Single(apiDescription.ParameterDescriptions);
                Assert.Equal(typeof(int), param.Type);
                Assert.Equal(typeof(int), param.ModelMetadata.ModelType);
                Assert.Equal(BindingSource.Path, param.Source);
            }

            AssertPathParameter(GetApiDescription((int foo) => { }, "/{foo}"));
            AssertPathParameter(GetApiDescription(([FromRoute] int foo) => { }));
        }

        [Fact]
        public void AddsFromQueryParameterAsQuery()
        {
            static void AssertQueryParameter(ApiDescription apiDescription)
            {
                var param = Assert.Single(apiDescription.ParameterDescriptions);
                Assert.Equal(typeof(int), param.Type);
                Assert.Equal(typeof(int), param.ModelMetadata.ModelType);
                Assert.Equal(BindingSource.Query, param.Source);
            }

            AssertQueryParameter(GetApiDescription((int foo) => { }, "/"));
            AssertQueryParameter(GetApiDescription(([FromQuery] int foo) => { }));
        }

        [Fact]
        public void AddsFromHeaderParameterAsHeader()
        {
            var apiDescription = GetApiDescription(([FromHeader] int foo) => { });
            var param = Assert.Single(apiDescription.ParameterDescriptions);

            Assert.Equal(typeof(int), param.Type);
            Assert.Equal(typeof(int), param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Header, param.Source);
        }

        [Fact]
        public void DoesNotAddFromServiceParameterAsService()
        {
            Assert.Empty(GetApiDescription((IInferredServiceInterface foo) => { }).ParameterDescriptions);
            Assert.Empty(GetApiDescription(([FromServices] int foo) => { }).ParameterDescriptions);
            Assert.Empty(GetApiDescription((HttpContext context) => { }).ParameterDescriptions);
            Assert.Empty(GetApiDescription((HttpRequest request) => { }).ParameterDescriptions);
            Assert.Empty(GetApiDescription((HttpResponse response) => { }).ParameterDescriptions);
            Assert.Empty(GetApiDescription((ClaimsPrincipal user) => { }).ParameterDescriptions);
            Assert.Empty(GetApiDescription((CancellationToken token) => { }).ParameterDescriptions);
        }

        [Fact]
        public void AddsFromBodyParameterAsBody()
        {
            static void AssertBodyParameter(ApiDescription apiDescription, Type expectedType)
            {
                var param = Assert.Single(apiDescription.ParameterDescriptions);
                Assert.Equal(expectedType, param.Type);
                Assert.Equal(expectedType, param.ModelMetadata.ModelType);
                Assert.Equal(BindingSource.Body, param.Source);
            }

            AssertBodyParameter(GetApiDescription((Todo foo) => { }), typeof(Todo));
            AssertBodyParameter(GetApiDescription(([FromBody] int foo) => { }), typeof(int));
        }

        [Fact]
        public void AddsDefaultValueFromParameters()
        {
            var apiDescription = GetApiDescription(TestActionWithDefaultValue);

            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(42, param.DefaultValue);
        }

        [Fact]
        public void AddsMultipleParameters()
        {
            var apiDescription = GetApiDescription(([FromRoute] int foo, int bar, Todo fromBody) => { });
            Assert.Equal(3, apiDescription.ParameterDescriptions.Count);

            var fooParam = apiDescription.ParameterDescriptions[0];
            Assert.Equal(typeof(int), fooParam.Type);
            Assert.Equal(typeof(int), fooParam.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Path, fooParam.Source);
            Assert.True(fooParam.IsRequired);

            var barParam = apiDescription.ParameterDescriptions[1];
            Assert.Equal(typeof(int), barParam.Type);
            Assert.Equal(typeof(int), barParam.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Query, barParam.Source);
            Assert.True(barParam.IsRequired);

            var fromBodyParam = apiDescription.ParameterDescriptions[2];
            Assert.Equal(typeof(Todo), fromBodyParam.Type);
            Assert.Equal(typeof(Todo), fromBodyParam.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Body, fromBodyParam.Source);
            Assert.True(fromBodyParam.IsRequired);
        }

        [Fact]
        public void TestParameterIsRequired()
        {
            var apiDescription = GetApiDescription(([FromRoute] int foo, int? bar) => { });
            Assert.Equal(2, apiDescription.ParameterDescriptions.Count);

            var fooParam = apiDescription.ParameterDescriptions[0];
            Assert.Equal(typeof(int), fooParam.Type);
            Assert.Equal(typeof(int), fooParam.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Path, fooParam.Source);
            Assert.True(fooParam.IsRequired);

            var barParam = apiDescription.ParameterDescriptions[1];
            Assert.Equal(typeof(int?), barParam.Type);
            Assert.Equal(typeof(int?), barParam.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Query, barParam.Source);
            Assert.False(barParam.IsRequired);
        }

#nullable enable

        [Fact]
        public void TestIsRequiredFromBody()
        {
            var apiDescription0 = GetApiDescription(([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Todo fromBody) => { });
            var apiDescription1 = GetApiDescription((Todo? fromBody) => { });
            Assert.Equal(1, apiDescription0.ParameterDescriptions.Count);
            Assert.Equal(1, apiDescription1.ParameterDescriptions.Count);

            var fromBodyParam0 = apiDescription0.ParameterDescriptions[0];
            Assert.Equal(typeof(Todo), fromBodyParam0.Type);
            Assert.Equal(typeof(Todo), fromBodyParam0.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Body, fromBodyParam0.Source);
            Assert.False(fromBodyParam0.IsRequired);

            var fromBodyParam1 = apiDescription1.ParameterDescriptions[0];
            Assert.Equal(typeof(Todo), fromBodyParam1.Type);
            Assert.Equal(typeof(Todo), fromBodyParam1.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Body, fromBodyParam1.Source);
            Assert.False(fromBodyParam1.IsRequired);
        }

        // This is necessary for TestIsRequiredFromBody to pass until https://github.com/dotnet/roslyn/issues/55254 is resolved.
        private object RandomMethod() => throw new NotImplementedException();

#nullable disable

        [Fact]
        public void AddsDisplayNameFromRouteEndpoint()
        {
            var apiDescription = GetApiDescription(() => "foo", displayName: "FOO");

            Assert.Equal("FOO", apiDescription.ActionDescriptor.DisplayName);
        }

        [Fact]
        public void AddsMetadataFromRouteEndpoint()
        {
            var apiDescription = GetApiDescription([ApiExplorerSettings(IgnoreApi = true)]() => { });

            Assert.NotEmpty(apiDescription.ActionDescriptor.EndpointMetadata);

            var apiExplorerSettings = apiDescription.ActionDescriptor.EndpointMetadata
                .OfType<ApiExplorerSettingsAttribute>()
                .FirstOrDefault();

            Assert.NotNull(apiExplorerSettings);
            Assert.True(apiExplorerSettings.IgnoreApi);
        }

        [Fact]
        public void RespectsProducesProblemExtensionMethod()
        {
            // Arrange
            var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(null));
            builder.MapGet("/api/todos", () => "").ProducesProblem(StatusCodes.Status400BadRequest);
            var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

            var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
            var hostEnvironment = new HostEnvironment
            {
                ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
            };
            var provider = new EndpointMetadataApiDescriptionProvider(endpointDataSource, hostEnvironment, new ServiceProviderIsService());

            // Act
            provider.OnProvidersExecuting(context);

            // Assert
            var apiDescription = Assert.Single(context.Results);
            var responseTypes = Assert.Single(apiDescription.SupportedResponseTypes);
            Assert.Equal(typeof(ProblemDetails), responseTypes.Type);
        }

        [Fact]
        public void RespectsProducesWithGroupNameExtensionMethod()
        {
            // Arrange
            var endpointGroupName = "SomeEndpointGroupName";
            var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(null));
            builder.MapGet("/api/todos", () => "").Produces<Todo>().WithGroupName(endpointGroupName);
            var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

            var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
            var hostEnvironment = new HostEnvironment
            {
                ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
            };
            var provider = new EndpointMetadataApiDescriptionProvider(endpointDataSource, hostEnvironment, new ServiceProviderIsService());

            // Act
            provider.OnProvidersExecuting(context);

            // Assert
            var apiDescription = Assert.Single(context.Results);
            var responseTypes = Assert.Single(apiDescription.SupportedResponseTypes);
            Assert.Equal(typeof(Todo), responseTypes.Type);
            Assert.Equal(endpointGroupName, apiDescription.GroupName);
        }

        [Fact]
        public void RespectsExcludeFromDescription()
        {
            // Arrange
            var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(null));
            builder.MapGet("/api/todos", () => "").Produces<Todo>().ExcludeFromDescription();
            var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

            var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
            var hostEnvironment = new HostEnvironment
            {
                ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
            };
            var provider = new EndpointMetadataApiDescriptionProvider(endpointDataSource, hostEnvironment, new ServiceProviderIsService());

            // Act
            provider.OnProvidersExecuting(context);

            // Assert
            Assert.Empty(context.Results);
        }

        [Fact]
        public void HandlesProducesWithProducesProblem()
        {
            // Arrange
            var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(null));
            builder.MapGet("/api/todos", () => "")
                .Produces<Todo>(StatusCodes.Status200OK)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
            var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

            var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
            var hostEnvironment = new HostEnvironment
            {
                ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
            };
            var provider = new EndpointMetadataApiDescriptionProvider(endpointDataSource, hostEnvironment, new ServiceProviderIsService());

            // Act
            provider.OnProvidersExecuting(context);
            provider.OnProvidersExecuted(context);

            // Assert
            Assert.Collection(
                context.Results.SelectMany(r => r.SupportedResponseTypes).OrderBy(r => r.StatusCode),
                responseType =>
                {
                    Assert.Equal(typeof(Todo), responseType.Type);
                    Assert.Equal(200, responseType.StatusCode);
                    Assert.Equal(new[] { "application/json" }, GetSortedMediaTypes(responseType));

                },
                responseType =>
                {
                    Assert.Equal(typeof(HttpValidationProblemDetails), responseType.Type);
                    Assert.Equal(400, responseType.StatusCode);
                    Assert.Equal(new[] { "application/validationproblem+json" }, GetSortedMediaTypes(responseType));
                },
                responseType =>
                {
                    Assert.Equal(typeof(ProblemDetails), responseType.Type);
                    Assert.Equal(404, responseType.StatusCode);
                    Assert.Equal(new[] { "application/problem+json" }, GetSortedMediaTypes(responseType));
                },
                responseType =>
                {
                    Assert.Equal(typeof(ProblemDetails), responseType.Type);
                    Assert.Equal(409, responseType.StatusCode);
                    Assert.Equal(new[] { "application/problem+json" }, GetSortedMediaTypes(responseType));
                });
        }

        [Fact]
        public void HandleMultipleProduces()
        {
            // Arrange
            var app = new TestEndpointRouteBuilder(new ApplicationBuilder(null));

            app.MapGet("/todo/{id}", (int id) =>
                {
                    if (id != 0)
                    {
                        return Results.NotFound();
                    }

                    return Results.Ok(new Todo(0, "Test", false));
                })
                .Produces<MinimalActionEndpointConventionBuilder, Todo[]>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status404NotFound);

            app.MapGet("/todo/{id}", async (context) =>
                {
                    if ((string)context.Request.RouteValues["id"] != "0")
                    {
                        context.Response.StatusCode = 404;
                        return;
                    }

                    await context.Response.WriteAsync(@"{""Id"": 0, ""Name"": ""Test"", ""IsCompleted"": false}");
                })
                .Produces<IEndpointConventionBuilder, Todo[]>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status404NotFound);

            app.MapControllers().Produces(StatusCodes.Status200OK);

            var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

            var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
            var hostEnvironment = new HostEnvironment
            {
                ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
            };
            var provider = new EndpointMetadataApiDescriptionProvider(endpointDataSource, hostEnvironment, new ServiceProviderIsService());

            // Act
            provider.OnProvidersExecuting(context);
            provider.OnProvidersExecuted(context);

            // Assert
            Assert.Collection(
                context.Results.SelectMany(r => r.SupportedResponseTypes).OrderBy(r => r.StatusCode),
                responseType =>
                {
                    Assert.Equal(typeof(Todo), responseType.Type);
                    Assert.Equal(200, responseType.StatusCode);
                    Assert.Equal(new[] { "application/json" }, GetSortedMediaTypes(responseType));

                },
                responseType =>
                {
                    Assert.Equal(typeof(Todo), responseType.Type);
                    Assert.Equal(201, responseType.StatusCode);
                    Assert.Equal(new[] { "application/json" }, GetSortedMediaTypes(responseType));
                });
        }

        private static IEnumerable<string> GetSortedMediaTypes(ApiResponseType apiResponseType)
        {
            return apiResponseType.ApiResponseFormats
                .OrderBy(format => format.MediaType)
                .Select(format => format.MediaType);
        }

        private IList<ApiDescription> GetApiDescriptions(
            Delegate action,
            string pattern = null,
            IEnumerable<string> httpMethods = null,
            string displayName = null)
        {
            var methodInfo = action.Method;
            var attributes = methodInfo.GetCustomAttributes();
            var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

            var httpMethodMetadata = new HttpMethodMetadata(httpMethods ?? new[] { "GET" });
            var metadataItems = new List<object>(attributes) { methodInfo, httpMethodMetadata };
            var endpointMetadata = new EndpointMetadataCollection(metadataItems.ToArray());
            var routePattern = RoutePatternFactory.Parse(pattern ?? "/");

            var endpoint = new RouteEndpoint(httpContext => Task.CompletedTask, routePattern, 0, endpointMetadata, displayName);
            var endpointDataSource = new DefaultEndpointDataSource(endpoint);
            var hostEnvironment = new HostEnvironment
            {
                ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
            };

            var provider = new EndpointMetadataApiDescriptionProvider(endpointDataSource, hostEnvironment, new ServiceProviderIsService());

            provider.OnProvidersExecuting(context);
            provider.OnProvidersExecuted(context);

            return context.Results;
        }

        private ApiDescription GetApiDescription(Delegate action, string pattern = null, string displayName = null) =>
            Assert.Single(GetApiDescriptions(action, pattern, displayName: displayName));

        private static void TestAction()
        {
        }

        private static void TestActionWithDefaultValue(int foo = 42)
        {
        }

        private record Todo(int Id, string Name, bool IsCompleted);

        private interface IInferredServiceInterface
        {
        }

        private interface IInferredJsonInterface
        {
        }

        private class ServiceProviderIsService : IServiceProviderIsService
        {
            public bool IsService(Type serviceType) => serviceType == typeof(IInferredServiceInterface);
        }

        private class HostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }
        }

        private class TestEndpointRouteBuilder : IEndpointRouteBuilder
        {
            public TestEndpointRouteBuilder(IApplicationBuilder applicationBuilder)
            {
                ApplicationBuilder = applicationBuilder ?? throw new ArgumentNullException(nameof(applicationBuilder));
                DataSources = new List<EndpointDataSource>();
            }

            public IApplicationBuilder ApplicationBuilder { get; }

            public IApplicationBuilder CreateApplicationBuilder() => ApplicationBuilder.New();

            public ICollection<EndpointDataSource> DataSources { get; }

            public IServiceProvider ServiceProvider => ApplicationBuilder.ApplicationServices;
        }
    }
}
