// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.SemanticKernel.Memory;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using StackExchange.Redis;
using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

/// <summary>
/// Does setup and teardown of redis docker container and associated test data.
/// </summary>
public class RedisMemoryFixture : IAsyncLifetime
{
    /// <summary>The docker client we are using to create a redis container with.</summary>
    private readonly DockerClient _client;

    /// <summary>The id of the redis container that we are testing with.</summary>
    private string? _containerId = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisMemoryFixture"/> class.
    /// </summary>
    public RedisMemoryFixture()
    {
        using var dockerClientConfiguration = new DockerClientConfiguration();
        this._client = dockerClientConfiguration.CreateClient();
    }

    /// <summary>Gets the redis database connection to use for tests.</summary>
    public IDatabase Database { get; private set; }

    /// <summary>
    /// Create / Recreate redis docker container and run it.
    /// </summary>
    /// <returns>An async task.</returns>
    public async Task InitializeAsync()
    {
        this._containerId = await SetupRedisContainerAsync(this._client);

        // Connect to redis.
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        this.Database = redis.GetDatabase();

        // Create a schema for the vector store.
        var schema = new Schema();
        schema.AddTextField("HotelName");
        schema.AddNumericField("hotelCode");
        schema.AddTextField("Description");
        schema.AddVectorField("DescriptionEmbeddings", Schema.VectorField.VectorAlgo.HNSW, new Dictionary<string, object>()
        {
            ["TYPE"] = "FLOAT32",
            ["DIM"] = "4",
            ["DISTANCE_METRIC"] = "L2"
        });
        var createParams = new FTCreateParams();
        createParams.AddPrefix("hotels");
        await this.Database.FT().CreateAsync("hotels", createParams, schema);

        // Create some test data.
#pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random();
        var address = new HotelAddress("Seattle", "USA");
        var embeddings = Enumerable.Range(1, 4).Select(_ => (float)random.NextSingle()).ToArray();

        await this.Database.JSON().SetAsync("hotels:H10", "$", new { HotelName = "My Hotel 10", hotelCode = 10, Seafront = true, Address = address, Description = "This is a great hotel.", DescriptionEmbeddings = embeddings });
        await this.Database.JSON().SetAsync("hotels:H11", "$", new { HotelName = "My Hotel 11", hotelCode = 11, Seafront = false, Description = "This is a great hotel.", DescriptionEmbeddings = embeddings });
        await this.Database.JSON().SetAsync("hotels:H12", "$", new { HotelName = "My Hotel 12", hotelCode = 12, Seafront = false, Description = "This is a great hotel.", DescriptionEmbeddings = embeddings });
        await this.Database.JSON().SetAsync("hotels:H13-Invalid", "$", new { HotelId = "AnotherId", HotelName = "My Invalid Hotel", hotelCode = 12, Seafront = false, Description = "This is an invalid hotel.", DescriptionEmbeddings = embeddings });
#pragma warning restore CA5394 // Do not use insecure randomness
    }

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
    /// Setup the redis container by pulling the image and running it.
    /// </summary>
    /// <param name="client">The docker client to create the container with.</param>
    /// <returns>The id of the container.</returns>
    private static async Task<string> SetupRedisContainerAsync(DockerClient client)
    {
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = "redis/redis-stack",
                Tag = "latest",
            },
            null,
            new Progress<JSONMessage>());

        var container = await client.Containers.CreateContainerAsync(new CreateContainerParameters()
        {
            Image = "redis/redis-stack",
            HostConfig = new HostConfig()
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    {"6379", new List<PortBinding> {new() {HostPort = "6379"}}}
                },
                PublishAllPorts = true
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                { "6379", default }
            },
        });

        await client.Containers.StartContainerAsync(
            container.ID,
            new ContainerStartParameters());

        return container.ID;
    }

    /// <summary>
    /// A test model for the vector store.
    /// </summary>
    /// <param name="HotelId">The key of the record.</param>
    /// <param name="HotelName">A string metadata field.</param>
    /// <param name="HotelCode">An int metadata field.</param>
    /// <param name="Seafront">A bool metadata field.</param>
    /// <param name="Address">A complex type metadata field.</param>
    /// <param name="Description">A document field.</param>
    /// <param name="DescriptionEmbeddings">A vector field.</param>
    public record HotelInfo(
        [property: MemoryRecordKey] string HotelId,
        [property: MemoryRecordData] string HotelName,
        [property: MemoryRecordData, JsonPropertyName("hotelCode")] int HotelCode,
        [property: MemoryRecordData] bool Seafront,
        [property: MemoryRecordData] HotelAddress Address,
        [property: MemoryRecordData(HasEmbedding = true, EmbeddingPropertyName = "DescriptionEmbeddings")] string Description,
        [property: MemoryRecordVector()] ReadOnlyMemory<float>? DescriptionEmbeddings);

    /// <summary>
    /// A test model for the vector store to simulate a complex type.
    /// </summary>
    /// <param name="City">A string metadata field.</param>
    /// <param name="Country">A string metadata field.</param>
    public record HotelAddress(
        string City,
        string Country);
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
