// Copyright (c) Microsoft. All rights reserved.

using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

[CollectionDefinition("RedisMemoryCollection")]
public class RedisMemoryCollectionFixture : ICollectionFixture<RedisMemoryFixture>
{
}
