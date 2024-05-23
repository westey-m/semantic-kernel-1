// Copyright (c) Microsoft. All rights reserved.

using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

[CollectionDefinition("QdrantVectorDBCollection")]
public class QdrantVectorDBCollectionFixture : ICollectionFixture<QdrantVectorDBFixture>
{
}
