// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Microsoft.AspNetCore.Analyzers.Verifiers.CSharpAnalyzerVerifier<Microsoft.AspNetCore.Analyzers.Mvc.MvcAnalyzer>;

namespace Microsoft.AspNetCore.Analyzers.Mvc;

public partial class AuthorizeAttributeOverriddenTest
{
    private const string CommonPrefix = """
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

""";

    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnController_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
public class MyController
{
    [{|#0:Authorize|}]
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyController").WithLocation(0)
        );
    }

    [Fact]
    public async Task AllowAnonymousOnAction_AuthorizeOnController_NoDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[Authorize]
public class MyController
{
    [AllowAnonymous]
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnControllerBaseType_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
public class MyControllerBase
{
}

public class MyController : MyControllerBase
{
    [{|#0:Authorize|}]
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0)
        );
    }

    [Fact]
    public async Task AuthorizeOnActionControllerAndAction_AllowAnonymousOnControllerBaseType_HasMultipleDiagnostics()
    {
        // The closest Authorize attribute to the action reported if multiple could be considered overridden.
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
[{|#0:Authorize|}]
public class MyControllerBase
{
}

[{|#1:Authorize|}]
public class MyController : MyControllerBase
{
    [{|#2:Authorize|}]
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0),
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(1),
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(2)
        );
    }

    [Fact]
    public async Task AuthorizeOnController_AllowAnonymousOnControllerBaseType_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
public class MyControllerBase
{
}

[{|#0:Authorize|}]
public class MyController : MyControllerBase
{
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0)
        );
    }

    [Fact]
    public async Task AuthorizeOnControllerWithMultipleActions_AllowAnonymousOnControllerBaseType_HasSingleDiagnostic()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
public class MyControllerBase
{
}

[{|#0:Authorize|}]
public class MyController : MyControllerBase
{
    public object Get() => new();
    public object AnotherGet() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0)
        );
    }

    [Fact]
    public async Task AuthorizeOnControllerBaseTypeWithMultipleChildren_AllowAnonymousOnControllerBaseBaseType_HasSingleDiagnostic()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
public class MyControllerBaseBase
{
}

[{|#0:Authorize|}]
public class MyControllerBase : MyControllerBaseBase
{
}

public class MyController : MyControllerBase
{
    public object Get() => new();
    public object AnotherGet() => new();
}

public class MyOtherController : MyControllerBase
{
    public object Get() => new();
    public object AnotherGet() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase").WithLocation(0)
        );
    }

    [Fact]
    public async Task CustomAuthorizeOnAction_CustomAllowAnonymousOnController_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
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
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyController").WithLocation(0)
        );
    }

    [Fact]
    public async Task AuthorizeOnAction_NonInheritableAllowAnonymousOnController_NoDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[MyAllowAnonymous]
public class MyControllerBase
{
}

public class MyController : MyControllerBase
{
    [Authorize]
    public object Get() => new();
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MyAllowAnonymousAttribute : Attribute, IAllowAnonymous
{
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task CustomAuthorizeCombinedWithAllowAnonymousOnAction_AllowAnonymousOnController_NoDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
public class MyController
{
    [MyAuthorize]
    public object Get() => new();
}

public class MyAuthorizeAttribute : Attribute, IAuthorizeData, IAllowAnonymous
{
    public string? Policy { get; set; }
    public string? Roles { get; set; }
    public string? AuthenticationSchemes { get; set; }
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AuthorizeBeforeAllowAnonymousOnAction_AllowAnonymousOnController_NoDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
public class MyController
{
    [Authorize(AuthenticationSchemes = "foo")]
    [AllowAnonymous]
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AuthorizeAfterAllowAnonymousOnAction_NoAttributeOnController_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
public class MyController
{
    [AllowAnonymous]
    [{|#0:Authorize|}]
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyController.Get").WithLocation(0)
        );
    }

    [Fact]
    public async Task NoAttributeOnAction_AuthorizeBeforeAllowAnonymousOnController_NoDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[Authorize(AuthenticationSchemes = "foo")]
[AllowAnonymous]
public class MyController
{
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoAttributeOnAction_AuthorizeAfterAllowAnonymousOnController_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
[AllowAnonymous]
[{|#0:Authorize|}]
public class MyController
{
    public object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyController").WithLocation(0)
        );
    }

    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnBaseMethod_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
public class MyControllerBase
{
    [AllowAnonymous]
    public virtual object Get() => new();
}

public class MyController : MyControllerBase
{
    [{|#0:Authorize|}]
    public override object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase.Get").WithLocation(0)
        );
    }

    [Fact]
    public async Task AllowAnonymousOnVirtualBaseActionWithNoOverride_AuthorizeOnController_NoDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
public class MyControllerBase
{
    [AllowAnonymous]
    public virtual object Get() => new();
}

[Authorize]
public class MyController : MyControllerBase
{
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AllowAnonymousOnVirtualBaseActionButNotOverride_AuthorizeOnController_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
public class MyControllerBase
{
    [AllowAnonymous]
    public virtual object Get() => new();
}

[{|#0:Authorize|}]
public class MyController : MyControllerBase
{
    public override object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBase.Get").WithLocation(0)
        );
    }

    [Fact]
    public async Task AllowAnonymousOnVirtualBaseBaseActionButNotOverride_AuthorizeOnControllerBaseType_HasDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}

public class MyControllerBaseBase
{
    [AllowAnonymous]
    public virtual object Get() => new();
}

[{|#0:Authorize|}]
public class MyControllerBase : MyControllerBaseBase
{
}

public class MyController : MyControllerBase
{
    public override object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source,
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("MyControllerBaseBase.Get").WithLocation(0)
        );
    }

    [Fact]
    public async Task AllowAnonymousOnVirtualBaseActionAndOverride_AuthorizeOnController_NoDiagnostics()
    {
        var source = $$"""
{{CommonPrefix}}
public class MyControllerBase
{
    [AllowAnonymous]
    public virtual object Get() => new();
}

[Authorize]
public class MyController : MyControllerBase
{
    [AllowAnonymous]
    public override object Get() => new();
}
""";

        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
