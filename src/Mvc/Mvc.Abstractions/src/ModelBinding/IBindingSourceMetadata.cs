// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    /// <summary>
    /// Metadata which specifies the data source for model binding.
    /// </summary>
    public interface IBindingSourceMetadata : Http.Metadata.IBindingSourceMetadata
    {
        /// <summary>
        /// Gets the <see cref="BindingSource"/>. 
        /// </summary>
        /// <remarks>
        /// The <see cref="BindingSource"/> is metadata which can be used to determine which data
        /// sources are valid for model binding of a property or parameter.
        /// </remarks>
        new BindingSource? BindingSource { get; }

        Http.Metadata.BindingSource Http.Metadata.IBindingSourceMetadata.BindingSource
        {
            get => BindingSource?.Id switch
            {
                nameof(BindingSource.Path) => Http.Metadata.BindingSource.Route,
                nameof(BindingSource.Query) => Http.Metadata.BindingSource.Query,
                nameof(BindingSource.Header) => Http.Metadata.BindingSource.Header,
                nameof(BindingSource.Body) => Http.Metadata.BindingSource.Body,
                nameof(BindingSource.Form) => Http.Metadata.BindingSource.Form,
                nameof(BindingSource.Services) => Http.Metadata.BindingSource.Services,
                _ => Http.Metadata.BindingSource.Custom
            };
        }
    }
}
