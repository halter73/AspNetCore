// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Http.Metadata
{
    /// <summary>
    /// A metadata enum representing a source of data for model binding.
    /// </summary>
    public enum BindingSource
    {
        Route,
        Query,
        Header,
        Body,
        Form,
        Services,
        Custom
    }
}
