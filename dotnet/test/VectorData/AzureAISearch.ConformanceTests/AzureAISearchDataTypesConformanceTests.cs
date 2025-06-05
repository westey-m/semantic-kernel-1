// Copyright (c) Microsoft. All rights reserved.

using AzureAISearch.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace AzureAISearch.ConformanceTests;

[Collection("Sequential")]
public class AzureAISearchDataTypesConformanceTests(AzureAISearchDataTypesConformanceTests.Fixture fixture) : DataTypesConformanceTests<string>(fixture), IClassFixture<AzureAISearchDataTypesConformanceTests.Fixture>
{
    public new class Fixture : DataTypesConformanceTests<string>.Fixture
    {
        public override ICollection<Type> SupportedDataTypes => [typeof(long), typeof(float), typeof(double), typeof(DateTimeOffset)];

        public override TestStore TestStore => AzureAISearchTestStore.Instance;
    }
}
