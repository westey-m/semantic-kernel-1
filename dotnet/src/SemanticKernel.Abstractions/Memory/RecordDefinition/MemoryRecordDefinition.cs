// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of the fields of a record stored in a memory store, plus how the fields are used.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class MemoryRecordDefinition
{
    /// <summary>Empty static list for initialization purposes.</summary>
    private static readonly List<Field> s_emptyFields = new();

    /// <summary>
    /// The list of fields that are stored in the record.
    /// </summary>
    public IReadOnlyList<Field> Fields { get; set; } = s_emptyFields;
}
