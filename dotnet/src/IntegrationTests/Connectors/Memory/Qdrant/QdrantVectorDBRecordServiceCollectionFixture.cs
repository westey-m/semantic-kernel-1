// Copyright (c) Microsoft. All rights reserved.

using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

[CollectionDefinition("QdrantVectorDBRecordServiceCollection")]
public class QdrantVectorDBRecordServiceCollectionFixture : ICollectionFixture<QdrantVectorDBRecordServiceFixture>
{
}
