// Copyright (c) Microsoft. All rights reserved.

using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

[CollectionDefinition("QdrantMemoryCollection")]
public class QdrantMemoryCollectionFixture : ICollectionFixture<QdrantMemoryFixture>
{
}
