// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Attribute to mark a property on a vector model class as the data that is being indexed.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class VectorStoreModelDataAttribute : Attribute
{
}
