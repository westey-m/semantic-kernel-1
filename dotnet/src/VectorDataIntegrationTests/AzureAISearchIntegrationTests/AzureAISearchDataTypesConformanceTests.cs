// Copyright (c) Microsoft. All rights reserved.

using AzureAISearchIntegrationTests.Support;
using VectorDataSpecificationTests;
using VectorDataSpecificationTests.Support;
using Xunit;

namespace AzureAISearchIntegrationTests;

[Collection("Sequential")]
public class AzureAISearchDataTypesConformanceTests(AzureAISearchDataTypesConformanceTests.Fixture fixture) : DataTypesConformanceTests<string>(fixture), IClassFixture<AzureAISearchDataTypesConformanceTests.Fixture>
{
    public new class Fixture : DataTypesConformanceTests<string>.Fixture
    {
        public override ICollection<Type> SupportedDataTypes => [typeof(long), typeof(float), typeof(double), typeof(DateTimeOffset)];

        public override TestStore TestStore => AzureAISearchTestStore.Instance;
    }
}
