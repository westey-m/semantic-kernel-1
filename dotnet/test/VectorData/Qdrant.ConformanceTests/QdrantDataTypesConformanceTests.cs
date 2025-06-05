// Copyright (c) Microsoft. All rights reserved.

using Qdrant.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace Qdrant.ConformanceTests;

public class QdrantDataTypesConformanceTests_NamedVectors(QdrantDataTypesConformanceTests_NamedVectors.Fixture fixture) : DataTypesConformanceTests<ulong>(fixture), IClassFixture<QdrantDataTypesConformanceTests_NamedVectors.Fixture>
{
    public new class Fixture : DataTypesConformanceTests<ulong>.Fixture
    {
        public override ICollection<Type> SupportedDataTypes => [typeof(long), typeof(float), typeof(double), typeof(DateTime), typeof(DateTimeOffset)];

        public override TestStore TestStore => QdrantTestStore.NamedVectorsInstance;
    }
}

public class QdrantDataTypesConformanceTests_UnnamedVector(QdrantDataTypesConformanceTests_UnnamedVector.Fixture fixture) : DataTypesConformanceTests<ulong>(fixture), IClassFixture<QdrantDataTypesConformanceTests_UnnamedVector.Fixture>
{
    public new class Fixture : DataTypesConformanceTests<ulong>.Fixture
    {
        public override ICollection<Type> SupportedDataTypes => [typeof(long), typeof(float), typeof(double), typeof(DateTime), typeof(DateTimeOffset)];

        public override TestStore TestStore => QdrantTestStore.UnnamedVectorInstance;
    }
}
