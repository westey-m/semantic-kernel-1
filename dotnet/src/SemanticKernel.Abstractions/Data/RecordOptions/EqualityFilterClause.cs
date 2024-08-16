// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// FilterClause which filters using equality of a field value.
/// </summary>
/// <remarks>
/// Constructs an instance of <see cref="EqualityFilterClause"/>
/// </remarks>
/// <param name="fieldName">Field name.</param>
/// <param name="value">Field value.</param>
[Experimental("SKEXP0001")]
public sealed class EqualityFilterClause(string fieldName, object value) : FilterClause(FilterClauseType.Equality)
{
    /// <summary>
    /// Fieled name to match.
    /// </summary>
    public string FieldName { get; private set; } = fieldName;

    /// <summary>
    /// Field value to match.
    /// </summary>
    public object Value { get; private set; } = value;
}
