// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of a key field for storage in a memory store.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class KeyField : Field
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyField"/> class.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    public KeyField(string fieldName)
        : base(fieldName)
    {
    }
}
