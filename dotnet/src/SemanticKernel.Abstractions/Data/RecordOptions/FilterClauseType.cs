// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Enum representing the type of filter clause.
/// </summary>
public enum FilterClauseType
{
    /// <summary>
    /// The filter clause is an equality clause.
    /// </summary>
    Equality,

    /// <summary>
    /// The filter clause that checks if a list of values contains a specific value.
    /// </summary>
    TagListContains
}
