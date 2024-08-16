// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// FilterClause which filters by checking if a field consisting of a list of values contains a specific value.
/// </summary>
/// <remarks>
/// Constructs an instance of <see cref="EqualityFilterClause"/>
/// </remarks>
/// <param name="fieldName">The name of the field with the list of values.</param>
/// <param name="value">The value that the list should contain.</param>
[Experimental("SKEXP0001")]
public sealed class TagListContainsFilterClause(string fieldName, string value) : FilterClause(FilterClauseType.TagListContains)
{
    /// <summary>
    /// The name of the field with the list of values.
    /// </summary>
    public string FieldName { get; private set; } = fieldName;

    /// <summary>
    /// The value that the list should contain.
    /// </summary>
    public string Value { get; private set; } = value;
}
