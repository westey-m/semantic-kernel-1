// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains configuration for doing basic search filtering.
/// All options are combined with and.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class BasicSearchFilter
{
    /// <summary>
    /// Gets or sets a collection of property value pairs where each key is a property name and each value is the exact value that the property should match.
    /// Filter values are treated as exact matches and combined with and.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? ExactMatchPropertyValuePairs { get; init; }
}
