// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of a field for storage in a memory store.
/// </summary>
[Experimental("SKEXP0001")]
public abstract class Field
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Field"/> class.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    protected Field(string fieldName)
    {
        this.FieldName = fieldName;
    }

    /// <summary>
    /// Gets or sets the name of the field.
    /// </summary>
    public string FieldName { get; set; }
}
