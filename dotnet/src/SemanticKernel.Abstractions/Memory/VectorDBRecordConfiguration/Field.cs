// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of a field for storage in a vector store.
/// </summary>
[Experimental("SKEXP0001")]
public abstract class Field
{
    /// <summary>
    /// The name of the field.
    /// </summary>
    public string FieldName { get; set; }
}
