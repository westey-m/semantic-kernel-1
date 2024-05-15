// Copyright (c) Microsoft. All rights reserved.

using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

[CollectionDefinition("RedisVectorDBRecordServiceCollection")]
public class RedisVectorDBRecordServiceCollectionFixture : ICollectionFixture<RedisVectorDBRecordServiceFixture>
{
}
