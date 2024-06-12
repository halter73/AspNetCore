// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Microsoft.AspNetCore.Analyzers.Verifiers.CSharpAnalyzerVerifier<Microsoft.AspNetCore.Analyzers.Mvc.MvcAnalyzer>;

namespace Microsoft.AspNetCore.Analyzers.Mvc;

public partial class AuthorizeAttributeOverriddenTest
{
    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnController_HasDiagnostics()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

[AllowAnonymous]
public class MyController
{
    [{|#0:Authorize|}]
    public object Get() => new();
}";

        var expectedDiagnostics = new[] {
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyController").WithLocation(0),
        };

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source, expectedDiagnostics);
    }

    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnControllerParentType_HasDiagnostics()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

[AllowAnonymous]
public class MyControllerBase
{
}

public class MyController : MyControllerBase
{
    [{|#0:Authorize|}]
    public object Get() => new();
}";

        var expectedDiagnostics = new[] {
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0),
        };

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source, expectedDiagnostics);
    }

    [Fact]
    public async Task CustomAuthorizeOnAction_CustomAllowAnonymousOnController_HasDiagnostics()
    {
        // Arrange
        var source = @"
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

[MyAllowAnonymous]
public class MyController
{
    [{|#0:MyAuthorize|}]
    public object Get() => new();
}


public class MyAuthorizeAttribute : Attribute, IAuthorizeData
{
    public string? Policy { get; set; }
    public string? Roles { get; set; }
    public string? AuthenticationSchemes { get; set; }
}

public class MyAllowAnonymousAttribute : Attribute, IAllowAnonymous
{
}
";

        var expectedDiagnostics = new[] {
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyController").WithLocation(0),
        };

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source, expectedDiagnostics);
    }

    [Fact]
    public async Task AuthorizeOnAction_NonInheritableAllowAnonymousOnController_NoDiagnostics()
    {
        // Arrange
        var source = @"
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

[MyAllowAnonymous]
public class MyControllerBase
{
}

public class MyController : MyControllerBase
{
    [{|#0:MyAuthorize|}]
    public object Get() => new();
}

public class MyAuthorizeAttribute : Attribute, IAuthorizeData
{
    public string? Policy { get; set; }
    public string? Roles { get; set; }
    public string? AuthenticationSchemes { get; set; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MyAllowAnonymousAttribute : Attribute, IAllowAnonymous
{
}
";

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnSameActionAfter_NoDiagnostics()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

[AllowAnonymous]
public class MyControllerBase
{
}

public class MyController : MyControllerBase
{
    [Authorize(AuthenticationSchemes = ""foo"")]
    [AllowAnonymous]
    public object Get() => new();
}";

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnSameActionBefore_HasDiagnostics()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

public class MyController
{
    [AllowAnonymous]
    [{|#0:Authorize(AuthenticationSchemes = ""foo"")|}]
    public object Get() => new();
}";

        var expectedDiagnostics = new[] {
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0),
        };

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source, expectedDiagnostics);
    }

    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnBaseMethod_HasDiagnostics()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

public class MyControllerBase
{
    [AllowAnonymous]
    public virtual object Get() => new();
}

public class MyController : MyControllerBase
{
    [{|#0:Authorize|}]
    public override object Get() => new();
}";

        var expectedDiagnostics = new[] {
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0),
        };

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source, expectedDiagnostics);
    }
}

