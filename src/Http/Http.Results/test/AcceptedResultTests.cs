// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Http.HttpResults;

public class AcceptedResultTests
{
    [Fact]
    public async Task ExecuteAsync_SetsStatusCodeAndLocationHeader()
    {
        // Arrange
        var expectedUrl = "testAction";
        var httpContext = GetHttpContext();

        // Act
        var result = new Accepted(expectedUrl);
        await result.ExecuteAsync(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, httpContext.Response.StatusCode);
        Assert.Equal(expectedUrl, httpContext.Response.Headers["Location"]);
    }

    [Fact]
    public void PopulateMetadata_AddsResponseTypeMetadata()
    {
        // Arrange
        Accepted MyApi() { throw new NotImplementedException(); }
        var metadata = new List<object>();
        var context = new EndpointMetadataContext(((Delegate)MyApi).GetMethodInfo(), metadata, EmptyServiceProvider.Instance);

        // Act
        PopulateMetadata<Accepted>(context);

        // Assert
        Assert.Contains(context.EndpointMetadata, m => m is ProducesResponseTypeMetadata { StatusCode: StatusCodes.Status202Accepted });
    }

    [Fact]
    public void ExecuteAsync_ThrowsArgumentNullException_WhenHttpContextIsNull()
    {
        // Arrange
        var result = new Accepted("location");
        HttpContext httpContext = null;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>("httpContext", () => result.ExecuteAsync(httpContext));
    }

    [Fact]
    public void PopulateMetadata_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>("context", () => PopulateMetadata<Accepted>(null));
    }

    [Fact]
    public void AcceptedResult_Implements_IStatusCodeHttpResult_Correctly()
    {
        // Act & Assert
        var result = Assert.IsAssignableFrom<IStatusCodeHttpResult>(new Accepted("location"));
        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
    }

    private static void PopulateMetadata<TResult>(MethodInfo method, EndpointBuilder builder)
        where TResult : IEndpointMetadataProvider => TResult.PopulateMetadata(method, builder);

    private static HttpContext GetHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = CreateServices();
        return httpContext;
    }

    private static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
