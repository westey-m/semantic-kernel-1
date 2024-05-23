// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory.CollectionConfiguration;

#pragma warning disable CA1720 // Identifier contains type name

/// <summary>
/// Defines the data types that vectors can be made up of.
/// </summary>
public enum VectorValueType
{
    /// <summary>
    /// A 4-byte / 32-bit floating point number.
    /// </summary>
    Float32,

    /// <summary>
    /// A 8-byte / 64-bit floating point number.
    /// </summary>
    Float64,

    /// <summary>
    /// A 1-byte / 8-bit unsigned integer.
    /// </summary>
    UInt8
}
#pragma warning restore CA1720 // Identifier contains type name
