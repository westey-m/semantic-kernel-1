// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Grpc.Core;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

public class QdrantVectorStoreFixture : IAsyncLifetime
{
    /// <summary>The docker client we are using to create a qdrant container with.</summary>
    private readonly DockerClient _client;

    /// <summary>The id of the qdrant container that we are testing with.</summary>
    private string? _containerId = null;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorStoreFixture"/> class.
    /// </summary>
    public QdrantVectorStoreFixture()
    {
        using var dockerClientConfiguration = new DockerClientConfiguration();
        this._client = dockerClientConfiguration.CreateClient();
    }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>Gets the qdrant client connection to use for tests.</summary>
    public QdrantClient QdrantClient { get; private set; }

#pragma warning disable CA5394 // Do not use insecure randomness

    /// <summary>
    /// Create / Recreate qdrant docker container and run it.
    /// </summary>
    /// <returns>An async task.</returns>
    public async Task InitializeAsync()
    {
        this._containerId = await SetupQdrantContainerAsync(this._client);

        // Connect to qdrant.
        this.QdrantClient = new QdrantClient("localhost");

        // Create schemas for the vector store.
        var vectorParamsMap = new VectorParamsMap();
        vectorParamsMap.Map.Add("DescriptionEmbeddings", new VectorParams { Size = 4, Distance = Distance.Cosine });

        // Wait for the qdrant container to be ready.
        var retryCount = 0;
        while (retryCount++ < 5)
        {
            try
            {
                await this.QdrantClient.ListCollectionsAsync();
            }
            catch (RpcException e)
            {
                if (e.StatusCode != Grpc.Core.StatusCode.Unavailable)
                {
                    throw;
                }

                await Task.Delay(1000);
            }
        }

        await this.QdrantClient.CreateCollectionAsync(
            "namedVectorsHotels",
            vectorParamsMap);

        await this.QdrantClient.CreateCollectionAsync(
            "singleVectorHotels",
            new VectorParams { Size = 4, Distance = Distance.Cosine });

        // Create some test data using named vectors.
        var random = new Random();

        var namedVectors1 = new NamedVectors();
        var namedVectors2 = new NamedVectors();
        var namedVectors3 = new NamedVectors();

        namedVectors1.Vectors.Add("DescriptionEmbeddings", Enumerable.Range(1, 4).Select(_ => (float)random.NextSingle()).ToArray());
        namedVectors2.Vectors.Add("DescriptionEmbeddings", Enumerable.Range(1, 4).Select(_ => (float)random.NextSingle()).ToArray());
        namedVectors3.Vectors.Add("DescriptionEmbeddings", Enumerable.Range(1, 4).Select(_ => (float)random.NextSingle()).ToArray());

        List<PointStruct> namedVectorPoints =
        [
            new PointStruct
            {
                Id = 1,
                Vectors = new Vectors { Vectors_ = namedVectors1 },
                Payload = { ["HotelName"] = "My Hotel 1", ["hotelCode"] = 1, ["Description"] = "This is a great hotel." }
            },
            new PointStruct
            {
                Id = 2,
                Vectors = new Vectors { Vectors_ = namedVectors2 },
                Payload = { ["HotelName"] = "My Hotel 2", ["hotelCode"] = 2, ["Description"] = "This is a great hotel." }
            },
            new PointStruct
            {
                Id = 3,
                Vectors = new Vectors { Vectors_ = namedVectors3 },
                Payload = { ["HotelName"] = "My Hotel 3", ["HotelCode"] = 3, ["Description"] = "This is a great hotel." }
            },
        ];

        await this.QdrantClient.UpsertAsync("namedVectorsHotels", namedVectorPoints);

        // Create some test data using a single unnamed vector.
        List<PointStruct> unnamedVectorPoints =
        [
            new PointStruct
            {
                Id = 11,
                Vectors = Enumerable.Range(1, 4).Select(_ => (float)random.NextSingle()).ToArray(),
                Payload = { ["HotelName"] = "My Hotel 11", ["hotelCode"] = 11, ["Description"] = "This is a great hotel." }
            },
            new PointStruct
            {
                Id = 12,
                Vectors = Enumerable.Range(1, 4).Select(_ => (float)random.NextSingle()).ToArray(),
                Payload = { ["HotelName"] = "My Hotel 12", ["hotelCode"] = 12, ["Description"] = "This is a great hotel." }
            },
            new PointStruct
            {
                Id = 13,
                Vectors = Enumerable.Range(1, 4).Select(_ => (float)random.NextSingle()).ToArray(),
                Payload = { ["HotelName"] = "My Hotel 13", ["hotelCode"] = 13, ["Description"] = "This is a great hotel." }
            },
        ];

        await this.QdrantClient.UpsertAsync("singleVectorHotels", unnamedVectorPoints);
    }

#pragma warning restore CA5394 // Do not use insecure randomness

    /// <summary>
    /// Delete the docker container after the test run.
    /// </summary>
    /// <returns>An async task.</returns>
    public async Task DisposeAsync()
    {
        if (this._containerId != null)
        {
            await this._client.Containers.StopContainerAsync(this._containerId, new ContainerStopParameters());
            await this._client.Containers.RemoveContainerAsync(this._containerId, new ContainerRemoveParameters());
        }
    }

    /// <summary>
    /// Setup the qdrant container by pulling the image and running it.
    /// </summary>
    /// <param name="client">The docker client to create the container with.</param>
    /// <returns>The id of the container.</returns>
    private static async Task<string> SetupQdrantContainerAsync(DockerClient client)
    {
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = "qdrant/qdrant",
                Tag = "latest",
            },
            null,
            new Progress<JSONMessage>());

        var container = await client.Containers.CreateContainerAsync(new CreateContainerParameters()
        {
            Image = "qdrant/qdrant",
            HostConfig = new HostConfig()
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    {"6333", new List<PortBinding> {new() {HostPort = "6333" } }},
                    {"6334", new List<PortBinding> {new() {HostPort = "6334" } }}
                },
                PublishAllPorts = true
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                { "6333", default },
                { "6334", default }
            },
        });

        await client.Containers.StartContainerAsync(
            container.ID,
            new ContainerStartParameters());

        return container.ID;
    }

    /// <summary>
    /// A test model for the qdrant vector store.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public record HotelInfo()
    {
        /// <summary>The key of the record.</summary>
        [VectorStoreModelKey]
        public string HotelId { get; init; }

        /// <summary>A string metadata field.</summary>
        [VectorStoreModelMetadata]
        public string? HotelName { get; set; }

        /// <summary>An int metadata field.</summary>
        [VectorStoreModelMetadata, JsonPropertyName("hotelCode")]
        public int HotelCode { get; set; }

        /// <summary>A bool metadata field.</summary>
        [VectorStoreModelMetadata]
        public bool Seafront { get; set; }

        /// <summary>A data field.</summary>
        [VectorStoreModelData]
        public string Description { get; set; }

        /// <summary>A vector field.</summary>
        [VectorStoreModelVector]
        public ReadOnlyMemory<float>? DescriptionEmbeddings { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
