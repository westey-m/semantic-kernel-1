// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Class that can create a new collection in an Azure AI Search service using a provided configuration.
/// </summary>
public class AzureAISearchVectorCollectionConfiguredCreate : IVectorCollectionCreate
{
    /// <inheritdoc />
    public Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
