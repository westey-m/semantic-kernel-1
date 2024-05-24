// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of a record stored in a vector store.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class VectorDBRecordConfiguration
{
    /// <summary>
    /// The list of fields that are stored in the record.
    /// </summary>
    public IReadOnlyList<Field> Fields { get; set; }
}
