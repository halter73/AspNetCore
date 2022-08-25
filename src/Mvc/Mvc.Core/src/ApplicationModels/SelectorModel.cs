// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace Microsoft.AspNetCore.Mvc.ApplicationModels;

/// <summary>
/// A type that represents a selector.
/// </summary>
public class SelectorModel
{
    /// <summary>
    /// Intializes a new <see cref="SelectorModel"/>.
    /// </summary>
    public SelectorModel()
    {
        ActionConstraints = new List<IActionConstraintMetadata>();
    }

    /// <summary>
    /// Intializes a new <see cref="SelectorModel"/>.
    /// </summary>
    /// <param name="other">The <see cref="SelectorModel"/> to copy from.</param>
    public SelectorModel(SelectorModel other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        ActionConstraints = new List<IActionConstraintMetadata>(other.ActionConstraints);

        if (other.AttributeRouteModel != null)
        {
            AttributeRouteModel = new AttributeRouteModel(other.AttributeRouteModel);
        }

        foreach (var metadataItem in other.EndpointMetadata)
        {
            EndpointMetadata.Add(metadataItem);
        }
    }

    /// <summary>
    /// The <see cref="AttributeRouteModel"/>.
    /// </summary>
    public AttributeRouteModel? AttributeRouteModel { get; set; }

    /// <summary>
    /// The list of <see cref="IActionConstraintMetadata"/>.
    /// </summary>
    public IList<IActionConstraintMetadata> ActionConstraints { get; }

    /// <summary>
    /// Gets the <see cref="EndpointMetadata"/> associated with the <see cref="SelectorModel"/>.
    /// </summary>
    public IList<object> EndpointMetadata => EndpointBuilder.Metadata;

    // EndpointBuilder.Metadata is not virtual, so we use that as our source of truth so we don't copy
    // back-and-forth when exposing this via EndpointMetadataConvention's calls to PopulateMetadata.
    internal EndpointBuilder EndpointBuilder { get; } = new SelectorModelEndpointBuilder();

    // We could call PopulateMetadata later in ActionEndpointFactory which is during the call to
    // RouteEndpointDataSource.Endpoints or GetGroupedEndpoints(). If we did, we could pass in the
    // real RouteEndpointBuilder instead of this synthesized EndpointBuilder, but then this inferred metadata
    // wouldn't be visible to other native MVC and filters.
    private sealed class SelectorModelEndpointBuilder : EndpointBuilder
    {
        public override Endpoint Build()
        {
            throw new NotSupportedException();
        }
    }
}
