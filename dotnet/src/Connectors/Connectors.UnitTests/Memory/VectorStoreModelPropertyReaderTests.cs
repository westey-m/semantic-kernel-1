// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.Memory;

public class VectorStoreModelPropertyReaderTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void FindPropertiesCanFindAllPropertiesOnSinglePropsModel(bool supportsMultipleVectors, bool useConfig)
    {
        // Act.
        var properties = useConfig ?
            MemoryServiceModelPropertyReader.FindProperties(typeof(SinglePropsModel), this._singlePropsDefinition, supportsMultipleVectors) :
            MemoryServiceModelPropertyReader.FindProperties(typeof(SinglePropsModel), supportsMultipleVectors);

        // Assert.
        Assert.Equal("Key", properties.keyProperty.Name);
        Assert.Single(properties.dataProperties);
        Assert.Single(properties.vectorProperties);
        Assert.Equal("Data", properties.dataProperties[0].Name);
        Assert.Equal("Vector", properties.vectorProperties[0].Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FindPropertiesCanFindAllPropertiesOnMultiPropsModel(bool useConfig)
    {
        // Act.
        var properties = useConfig ?
            MemoryServiceModelPropertyReader.FindProperties(typeof(MultiPropsModel), this._multiPropsDefinition, true) :
            MemoryServiceModelPropertyReader.FindProperties(typeof(MultiPropsModel), true);

        // Assert.
        Assert.Equal("Key", properties.keyProperty.Name);
        Assert.Equal(2, properties.dataProperties.Count);
        Assert.Equal(2, properties.vectorProperties.Count);
        Assert.Equal("Data1", properties.dataProperties[0].Name);
        Assert.Equal("Data2", properties.dataProperties[1].Name);
        Assert.Equal("Vector1", properties.vectorProperties[0].Name);
        Assert.Equal("Vector2", properties.vectorProperties[1].Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FindPropertiesThrowsForMultipleVectorsWithSingleVectorSupport(bool useConfig)
    {
        // Act.
        var ex = useConfig ?
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(MultiPropsModel), this._multiPropsDefinition, false)) :
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(MultiPropsModel), false));

        // Assert.
        var expectedMessage = useConfig ?
            "Multiple vector properties configured for type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+MultiPropsModel while only one is supported." :
            "Multiple vector properties found on type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+MultiPropsModel while only one is supported.";
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FindPropertiesThrowsOnMultipleKeyProperties(bool useConfig)
    {
        // Act.
        var ex = useConfig ?
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(MultiKeysModel), this._multiKeysDefinition, true)) :
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(MultiKeysModel), true));

        // Assert.
        var expectedMessage = useConfig ?
            "Multiple key properties configured for type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+MultiKeysModel." :
            "Multiple key properties found on type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+MultiKeysModel.";
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FindPropertiesThrowsOnNoKeyProperty(bool useConfig)
    {
        // Act.
        var ex = useConfig ?
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(NoKeyModel), this._noKeyDefinition, true)) :
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(NoKeyModel), true));

        // Assert.
        var expectedMessage = useConfig ?
            "No key property configured for type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+NoKeyModel." :
            "No key property found on type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+NoKeyModel.";
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FindPropertiesThrowsOnNoVectorPropertyWithSingleVectorSupport(bool useConfig)
    {
        // Act.
        var ex = useConfig ?
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(NoVectorModel), this._noVectorDefinition, false)) :
            Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(NoVectorModel), false));

        // Assert.
        var expectedMessage = useConfig ?
            "No vector property configured for type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+NoVectorModel." :
            "No vector property found on type SemanticKernel.Connectors.UnitTests.Memory.VectorStoreModelPropertyReaderTests+NoVectorModel.";
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData("Key", "MissingKey")]
    [InlineData("Data", "MissingData")]
    [InlineData("Vector", "MissingVector")]
    public void FindPropertiesUsingConfigThrowsForNotFoundProperties(string propertyType, string propertyName)
    {
        var missingKeyDefinition = new MemoryRecordDefinition { Properties = [new MemoryRecordKeyProperty(propertyName)]};
        var missingDataDefinition = new MemoryRecordDefinition { Properties = [new MemoryRecordDataProperty(propertyName)] };
        var missingVectorDefinition = new MemoryRecordDefinition { Properties = [new MemoryRecordVectorProperty(propertyName)] };

        var definition = propertyType switch
        {
            "Key" => missingKeyDefinition,
            "Data" => missingDataDefinition,
            "Vector" => missingVectorDefinition,
            _ => throw new ArgumentException("Invalid property type.")
        };

        Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.FindProperties(typeof(NoKeyModel), definition, false));
    }

    [Fact]
    public void VerifyPropertyTypesPassForAllowedTypes()
    {
        // Arrange.
        var properties = MemoryServiceModelPropertyReader.FindProperties(typeof(SinglePropsModel), true);

        // Act.
        MemoryServiceModelPropertyReader.VerifyPropertyTypes(properties.dataProperties, [typeof(string)], "Data");
    }

    [Fact]
    public void VerifyPropertyTypesFailsForDisallowedTypes()
    {
        // Arrange.
        var properties = MemoryServiceModelPropertyReader.FindProperties(typeof(SinglePropsModel), true);

        // Act.
        var ex = Assert.Throws<ArgumentException>(() => MemoryServiceModelPropertyReader.VerifyPropertyTypes(properties.dataProperties, [typeof(int), typeof(float)], "Data"));

        // Assert.
        Assert.Equal("Data properties must be one of the supported types: System.Int32, System.Single. Type of Data is System.String.", ex.Message);
    }

    private class NoKeyModel
    {
    }

    private readonly MemoryRecordDefinition _noKeyDefinition = new();

    private class NoVectorModel
    {
        [MemoryRecordKey]
        public string Key { get; set; } = string.Empty;
    }

    private readonly MemoryRecordDefinition _noVectorDefinition = new()
    {
        Properties =
        [
            new MemoryRecordKeyProperty("Key")
        ]
    };

    private class MultiKeysModel
    {
        [MemoryRecordKey]
        public string Key1 { get; set; } = string.Empty;

        [MemoryRecordKey]
        public string Key2 { get; set; } = string.Empty;
    }

    private readonly MemoryRecordDefinition _multiKeysDefinition = new()
    {
        Properties =
        [
            new MemoryRecordKeyProperty("Key1"),
            new MemoryRecordKeyProperty("Key2")
        ]
    };

    private class SinglePropsModel
    {
        [MemoryRecordKey]
        public string Key { get; set; } = string.Empty;

        [MemoryRecordData]
        public string Data { get; set; } = string.Empty;

        [MemoryRecordVector]
        public ReadOnlyMemory<float> Vector { get; set; }

        public string NotAnnotated { get; set; } = string.Empty;
    }

    private readonly MemoryRecordDefinition _singlePropsDefinition = new()
    {
        Properties =
        [
            new MemoryRecordKeyProperty("Key"),
            new MemoryRecordDataProperty("Data"),
            new MemoryRecordVectorProperty("Vector")
        ]
    };

    private class MultiPropsModel
    {
        [MemoryRecordKey]
        public string Key { get; set; } = string.Empty;

        [MemoryRecordData]
        public string Data1 { get; set; } = string.Empty;

        [MemoryRecordData]
        public string Data2 { get; set; } = string.Empty;

        [MemoryRecordVector]
        public ReadOnlyMemory<float> Vector1 { get; set; }

        [MemoryRecordVector]
        public ReadOnlyMemory<float> Vector2 { get; set; }

        public string NotAnnotated { get; set; } = string.Empty;
    }

    private readonly MemoryRecordDefinition _multiPropsDefinition = new()
    {
        Properties =
        [
            new MemoryRecordKeyProperty("Key"),
            new MemoryRecordDataProperty("Data1"),
            new MemoryRecordDataProperty("Data2"),
            new MemoryRecordVectorProperty("Vector1"),
            new MemoryRecordVectorProperty("Vector2")
        ]
    };
}
