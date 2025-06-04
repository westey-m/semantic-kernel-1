// Copyright (c) Microsoft. All rights reserved.

using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;
using VectorDataSpecificationTests.Support;
using VectorDataSpecificationTests.Xunit;
using Xunit;

namespace VectorDataSpecificationTests;

#pragma warning disable xUnit1026 // Theory methods should use all of their parameters
#pragma warning disable CA1000 // Do not declare static members on generic types

public abstract class DataTypesConformanceTests<TKey>(DataTypesConformanceTests<TKey>.Fixture fixture) where TKey : notnull
{
    [ConditionalTheory]
    [MemberData(nameof(AllTypesUniqueMemberData))]
    public async Task GetAsyncReturnsInsertedRecord<TPropertyType>(TPropertyType dummyProperty)
    {
        Type propertyType = typeof(TPropertyType);
        if (!fixture.SupportedDataTypes.Contains(propertyType))
        {
            // Skip test if the property type is not supported by the fixture.
            return;
        }

        // Arrange.
        var subFixture = (VectorStoreCollectionFixture<TKey, TypedDataRecord<TPropertyType>>)fixture.SubFixtures[propertyType];
        var collection = subFixture.Collection;
        var expectedRecord = subFixture.TestData[0];

        // Act.
        var received = await collection.GetAsync(expectedRecord.Id, new() { IncludeVectors = false });

        // Assert.
        expectedRecord.AssertEqual(received, false, false);
    }

    [ConditionalTheory]
    [MemberData(nameof(AllTypesUniqueMemberData))]
    public async Task UpsertAsyncCanUpdateExistingRecord<TPropertyType>(TPropertyType dummyProperty)
    {
        Type propertyType = typeof(TPropertyType);
        if (!fixture.SupportedDataTypes.Contains(propertyType))
        {
            // Skip test if the property type is not supported by the fixture.
            return;
        }

        // Arrange.
        var subFixture = (VectorStoreCollectionFixture<TKey, TypedDataRecord<TPropertyType>>)fixture.SubFixtures[propertyType];
        var collection = subFixture.Collection;
        var existingRecord = subFixture.TestData[1];

        TypedDataRecord<TPropertyType> updated = new()
        {
            Id = existingRecord.Id,
            Data = dummyProperty,
            Floats = new ReadOnlyMemory<float>(Enumerable.Repeat(0.5f, TypedDataRecord<TPropertyType>.DimensionCount).ToArray())
        };

        Assert.NotNull(await collection.GetAsync(existingRecord.Id));

        // Act.
        await collection.UpsertAsync(updated);

        // Assert.
        var received = await collection.GetAsync(existingRecord.Id);
        updated.AssertEqual(received, false, false);

        // Cleaup.
        await collection.UpsertAsync(existingRecord);
    }

    [ConditionalFact]
    public virtual Task Equal_With_String()
        => this.TestFilterAsync<string>(x => x.Data == "UsedByGetTests", expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task Equal_With_DateTimeOffset()
        => this.TestFilterAsync<DateTimeOffset>(x => x.Data == new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task GreaterThan_With_DateTimeOffset()
        => this.TestFilterAsync<DateTimeOffset>(x => x.Data > new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task GreaterThan_With_DateTimeOffset_Offset()
        => this.TestFilterAsync<DateTimeOffset>(x => x.Data > new DateTimeOffset(2025, 5, 15, 0, 0, 0, new TimeSpan(-1, 0, 0)), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task GreaterThanOrEqual_With_DateTimeOffset()
        => this.TestFilterAsync<DateTimeOffset>(x => x.Data >= new DateTimeOffset(2025, 5, 15, 0, 0, 1, TimeSpan.Zero), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task LessThan_With_DateTimeOffset()
        => this.TestFilterAsync<DateTimeOffset>(x => x.Data < new DateTimeOffset(2025, 5, 15, 0, 0, 1, TimeSpan.Zero), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task LessThan_With_DateTimeOffset_Offset()
        => this.TestFilterAsync<DateTimeOffset>(x => x.Data < new DateTimeOffset(2025, 5, 15, 0, 0, 1, new TimeSpan(-1, 0, 0)), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task LessThanOrEqual_With_DateTimeOffset()
        => this.TestFilterAsync<DateTimeOffset>(x => x.Data <= new DateTimeOffset(2025, 5, 15, 0, 0, 1, TimeSpan.Zero), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task Equal_With_DateTime_Local()
        => this.TestFilterAsync<DateTime>(x => x.Data == new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Local), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task Equal_With_DateTime_Utc()
        => this.TestFilterAsync<DateTime>(x => x.Data == new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task GreaterThan_With_DateTime_Utc()
        => this.TestFilterAsync<DateTime>(x => x.Data > new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task GreaterThanOrEqual_With_DateTime_Utc()
        => this.TestFilterAsync<DateTime>(x => x.Data >= new DateTime(2025, 5, 15, 0, 0, 1, DateTimeKind.Utc), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task LessThan_With_DateTime_Utc()
        => this.TestFilterAsync<DateTime>(x => x.Data < new DateTime(2025, 5, 15, 0, 0, 1, DateTimeKind.Utc), expectZeroResults: false, expectAllResults: false);

    [ConditionalFact]
    public virtual Task LessThanOrEqual_With_DateTime_Utc()
        => this.TestFilterAsync<DateTime>(x => x.Data <= new DateTime(2025, 5, 15, 0, 0, 1, DateTimeKind.Utc), expectZeroResults: false, expectAllResults: false);

    private async Task TestFilterAsync<TPropertyType>(
        Expression<Func<TypedDataRecord<TPropertyType>, bool>> filter,
        bool expectZeroResults = false,
        bool expectAllResults = false)
    {
        Type propertyType = typeof(TPropertyType);
        if (!fixture.SupportedDataTypes.Contains(propertyType))
        {
            // Skip test if the property type is not supported by the fixture.
            return;
        }

        var subFixture = (VectorStoreCollectionFixture<TKey, TypedDataRecord<TPropertyType>>)fixture.SubFixtures[propertyType];

        var expected = subFixture.TestData.AsQueryable().Where(filter).OrderBy(r => r.Id).ToList();

        if (expected.Count == 0 && !expectZeroResults)
        {
            Assert.Fail("The test returns zero results, and so may be unreliable");
        }
        else if (expectZeroResults && expected.Count != 0)
        {
            Assert.Fail($"{nameof(expectZeroResults)} was true, but the test returned {expected.Count} results.");
        }

        if (expected.Count == subFixture.TestData.Count && !expectAllResults)
        {
            Assert.Fail("The test returns all results, and so may be unreliable");
        }
        else if (expectAllResults && expected.Count != subFixture.TestData.Count)
        {
            Assert.Fail($"{nameof(expectAllResults)} was true, but the test returned {expected.Count} results instead of the expected {subFixture.TestData.Count}.");
        }

        // Execute the query against the vector store, once using the strongly typed filter
        // and once using the dynamic filter
        var actual = await subFixture.Collection.SearchAsync(
                new ReadOnlyMemory<float>([1, 2, 3]),
                top: subFixture.TestData.Count,
                new() { Filter = filter })
            .Select(r => r.Record).OrderBy(r => r.Id).ToListAsync();

        if (actual.Count != expected.Count)
        {
            Assert.Fail($"Expected {expected.Count} results, but got {actual.Count}");
        }

        foreach (var (e, a) in expected.Zip(actual, (e, a) => (e, a)))
        {
            e.AssertEqual(a, includeVectors: false, compareVectors: false);
        }
    }

    public static IEnumerable<object[]> AllTypesFirstRecordMemberData()
    {
        yield return new object[] { "UsedByGetTests" };
        yield return new object[] { 1 };
        yield return new object[] { 1L };
        yield return new object[] { 1.1f };
        yield return new object[] { 1.1 };
        yield return new object[] { false };
        yield return new object[] { new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc) };
        yield return new object[] { new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero) };
    }

    public static IEnumerable<object[]> AllTypesUniqueMemberData()
    {
        yield return new object[] { "differentValue" };
        yield return new object[] { 12 };
        yield return new object[] { 12L };
        yield return new object[] { 12.12f };
        yield return new object[] { 12.12 };
        yield return new object[] { true };
        yield return new object[] { new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc).AddDays(12) };
        yield return new object[] { new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero).AddDays(12) };
    }

    public abstract class Fixture : VectorStoreFixture
    {
        public Dictionary<Type, VectorStoreFixture> SubFixtures { get; }

        public abstract ICollection<Type> SupportedDataTypes { get; }

        protected Fixture()
        {
#pragma warning disable CA2214 // Do not call overridable methods in constructors
            this.SubFixtures = new Dictionary<Type, VectorStoreFixture>();
            this.SubFixtures.Add(typeof(string), new TypedFixture<string>(this.TestStore, this.FormatCollectionName("string"), ["UsedByGetTests", "UsedByUpdateTests", "UsedByDeleteTests", "UsedByDeleteBatchTests"]));
            this.SubFixtures.Add(typeof(int), new TypedFixture<int>(this.TestStore, this.FormatCollectionName("int"), [1, 2, 3, 4]));
            this.SubFixtures.Add(typeof(long), new TypedFixture<long>(this.TestStore, this.FormatCollectionName("long"), [1L, 2L, 3L, 4L]));
            this.SubFixtures.Add(typeof(float), new TypedFixture<float>(this.TestStore, this.FormatCollectionName("float"), [1.1f, 2.2f, 3.3f, 4.4f]));
            this.SubFixtures.Add(typeof(double), new TypedFixture<double>(this.TestStore, this.FormatCollectionName("double"), [1.1, 2.2, 3.3, 4.4]));
            this.SubFixtures.Add(typeof(bool), new TypedFixture<bool>(this.TestStore, this.FormatCollectionName("bool"), [true, false, true, false]));
            this.SubFixtures.Add(typeof(DateTime), new TypedFixture<DateTime>(this.TestStore, this.FormatCollectionName("datetimeoffset"), [new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc).AddDays(1), new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc).AddDays(2), new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc).AddDays(3)]));
            this.SubFixtures.Add(typeof(DateTimeOffset), new TypedFixture<DateTimeOffset>(this.TestStore, this.FormatCollectionName("datetimeoffset"), [new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero).AddDays(1), new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero).AddDays(2), new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero).AddDays(3)]));
#pragma warning restore CA2214 // Do not call overridable methods in constructors
        }

        public virtual string FormatCollectionName(string collectionName) => collectionName;

        public override async Task InitializeAsync()
        {
            foreach (KeyValuePair<Type, VectorStoreFixture> fixture in this.SubFixtures)
            {
                if (this.SupportedDataTypes.Contains(fixture.Key))
                {
                    // Initialize only the fixtures for supported data types.
                    await fixture.Value.InitializeAsync();
                }
            }

            await base.InitializeAsync();
        }

        public override async Task DisposeAsync()
        {
            foreach (KeyValuePair<Type, VectorStoreFixture> fixture in this.SubFixtures)
            {
                if (this.SupportedDataTypes.Contains(fixture.Key))
                {
                    // Dispose only the fixtures for supported data types.
                    await fixture.Value.DisposeAsync();
                }
            }

            await base.DisposeAsync();
        }
    }

    /// <summary>
    /// This class is for testing databases with a property with any supported type.
    /// </summary>
    private sealed class TypedDataRecord<TPropertyType>
    {
        public const int DimensionCount = 3;

        [VectorStoreKey(StorageName = "key")]
        public TKey Id { get; set; } = default!;

        [VectorStoreData(StorageName = "data", IsIndexed = true)]
        public TPropertyType Data { get; set; } = default!;

        [VectorStoreVector(DimensionCount, StorageName = "embedding")]
        public ReadOnlyMemory<float> Floats { get; set; }

        public void AssertEqual(TypedDataRecord<TPropertyType>? other, bool includeVectors, bool compareVectors)
        {
            Assert.NotNull(other);
            Assert.Equal(this.Id, other.Id);
            Assert.Equal(this.Data, other.Data);

            if (includeVectors)
            {
                Assert.Equal(this.Floats.Span.Length, other.Floats.Span.Length);

                if (compareVectors)
                {
                    Assert.True(this.Floats.Span.SequenceEqual(other.Floats.Span));
                }
            }
        }
    }

    /// <summary>
    /// Provides data and configuration for a model without data fields.
    /// </summary>
    private sealed class TypedFixture<TPropertyType> : VectorStoreCollectionFixture<TKey, TypedDataRecord<TPropertyType>>
    {
        private readonly TestStore _testStore;
        private readonly string _collectionName;
        private readonly TPropertyType[] _dataValues;

        public TypedFixture(TestStore testStore, string collectionName, TPropertyType[] dataValues)
        {
            if (dataValues.Length != 4)
            {
                throw new InvalidOperationException("The GetDataValues method must return exactly 4 items.");
            }

            this._testStore = testStore;
            this._collectionName = collectionName;
            this._dataValues = dataValues;
        }

        public override TestStore TestStore => this._testStore;

        public override string CollectionName => this._collectionName;

        protected override List<TypedDataRecord<TPropertyType>> BuildTestData()
        {
            var dataValues = this._dataValues;

            return new List<TypedDataRecord<TPropertyType>>
            {
                new()
                {
                    Id = this.GenerateNextKey<TKey>(),
                    Data = dataValues[0],
                    Floats = new ReadOnlyMemory<float>(Enumerable.Repeat(0.1f, TypedDataRecord<TPropertyType>.DimensionCount).ToArray())
                },
                new()
                {
                    Id = this.GenerateNextKey<TKey>(),
                    Data = dataValues[1],
                    Floats = new ReadOnlyMemory<float>(Enumerable.Repeat(0.2f, TypedDataRecord<TPropertyType>.DimensionCount).ToArray())
                },
                new()
                {
                    Id = this.GenerateNextKey<TKey>(),
                    Data = dataValues[2],
                    Floats = new ReadOnlyMemory<float>(Enumerable.Repeat(0.3f, TypedDataRecord<TPropertyType>.DimensionCount).ToArray())
                },
                new()
                {
                    Id = this.GenerateNextKey<TKey>(),
                    Data = dataValues[3],
                    Floats = new ReadOnlyMemory<float>(Enumerable.Repeat(0.4f, TypedDataRecord<TPropertyType>.DimensionCount).ToArray())
                }
            };
        }

        public override VectorStoreCollectionDefinition CreateRecordDefinition()
            => new()
            {
                Properties =
                [
                    new VectorStoreKeyProperty(nameof(TypedDataRecord<TPropertyType>.Id), typeof(TKey)) { StorageName = "key" },
                    new VectorStoreDataProperty(nameof(TypedDataRecord<TPropertyType>.Data), typeof(TPropertyType)) { StorageName = "data", IsIndexed = true },
                    new VectorStoreVectorProperty(nameof(TypedDataRecord<TPropertyType>.Floats), typeof(ReadOnlyMemory<float>), TypedDataRecord<TPropertyType>.DimensionCount)
                    {
                        StorageName = "embedding",
                        IndexKind = this.IndexKind,
                    }
                ]
            };

        protected override async Task WaitForDataAsync()
        {
            for (var i = 0; i < 20; i++)
            {
                var getOptions = new RecordRetrievalOptions { IncludeVectors = true };
                var results = await this.Collection.GetAsync([this.TestData[0].Id, this.TestData[1].Id, this.TestData[2].Id, this.TestData[3].Id], getOptions).ToArrayAsync();
                if (results.Length == 4 && results.All(r => r != null))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            throw new InvalidOperationException("Data did not appear in the collection within the expected time.");
        }

        public override async Task DisposeAsync()
        {
            await this.Collection.EnsureCollectionDeletedAsync();
            await base.DisposeAsync();
        }
    }
}
