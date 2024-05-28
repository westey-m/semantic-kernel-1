// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of a data field for storage in a memory store.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class DataField : Field
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataField"/> class.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    public DataField(string fieldName)
        : base(fieldName)
    {
    }
}
