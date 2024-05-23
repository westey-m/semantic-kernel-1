// Copyright (c) Microsoft. All rights reserved.

using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

[CollectionDefinition("RedisVectorDBCollection")]
public class RedisVectorDBCollectionFixture : ICollectionFixture<RedisVectorDBFixture>
{
}
