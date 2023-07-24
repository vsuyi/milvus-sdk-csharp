using System.Globalization;
using IO.Milvus.Grpc;

namespace IO.Milvus;

public partial class MilvusCollection
{
    /// <summary>
    /// Describes a collection, returning information about its configuration and schema.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public async Task<MilvusCollectionDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var request = new DescribeCollectionRequest { CollectionName = Name };

        if (DatabaseName is not null)
        {
            request.DbName = DatabaseName;
        }

        DescribeCollectionResponse response =
            await _client.InvokeAsync(_client.GrpcClient.DescribeCollectionAsync, request, r => r.Status,
                    cancellationToken)
                .ConfigureAwait(false);

        CollectionSchema milvusCollectionSchema = new()
        {
            Name = response.Schema.Name,
            Description = response.Schema.Description

            // Note that an AutoId previously existed at the schema level, but is not deprecated.
            // AutoId is now only defined at the field level.
        };

        foreach (Grpc.FieldSchema grpcField in response.Schema.Fields)
        {
            FieldSchema milvusField = new(
                grpcField.FieldID, grpcField.Name, (MilvusDataType)grpcField.DataType,
                (MilvusFieldState)grpcField.State, grpcField.IsPrimaryKey, grpcField.AutoID, grpcField.IsPartitionKey,
                grpcField.IsDynamic, grpcField.Description);

            foreach (Grpc.KeyValuePair parameter in grpcField.TypeParams)
            {
                switch (parameter.Key)
                {
                    case Constants.VarcharMaxLength:
                        milvusField.MaxLength = int.Parse(parameter.Value, CultureInfo.InvariantCulture);
                        break;

                    case Constants.VectorDim:
                        milvusField.Dimension = long.Parse(parameter.Value, CultureInfo.InvariantCulture);
                        break;

                    // TODO: Should we warn for unknown type params?
                }
            }

            // TODO: IndexParams

            milvusCollectionSchema.Fields.Add(milvusField);
        }

        Dictionary<string, IList<int>> startPositions = response.StartPositions.ToDictionary(
            kdp => kdp.Key,
            kdp => (IList<int>)kdp.Data.Select(static p => (int)p).ToList());

        return new MilvusCollectionDescription(
            response.Aliases,
            response.CollectionName,
            response.CollectionID,
            (ConsistencyLevel)response.ConsistencyLevel,
            response.CreatedUtcTimestamp,
            milvusCollectionSchema,
            response.ShardsNum,
            startPositions);
    }

    /// <summary>
    /// Renames a collection.
    /// </summary>
    /// <param name="newName">The new collection name.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public async Task RenameAsync(string newName, CancellationToken cancellationToken = default)
    {
        var request = new RenameCollectionRequest { OldName = Name, NewName = newName };

        if (DatabaseName is not null)
        {
            request.DbName = DatabaseName;
        }

        await _client.InvokeAsync(_client.GrpcClient.RenameCollectionAsync, request, cancellationToken)
            .ConfigureAwait(false);

        Name = newName;
    }

    /// <summary>
    /// Drops a collection.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public async Task DropAsync(CancellationToken cancellationToken = default)
    {
        var request = new DropCollectionRequest { CollectionName = Name };

        if (DatabaseName is not null)
        {
            request.DbName = DatabaseName;
        }

        await _client.InvokeAsync(_client.GrpcClient.DropCollectionAsync, request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves statistics for a collection.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public async Task<IDictionary<string, string>> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var request = new GetCollectionStatisticsRequest { CollectionName = Name };

        if (DatabaseName is not null)
        {
            request.DbName = DatabaseName;
        }

        GetCollectionStatisticsResponse response = await _client.InvokeAsync(
            _client.GrpcClient.GetCollectionStatisticsAsync,
            request,
            static r => r.Status, cancellationToken).ConfigureAwait(false);

        return response.Stats.ToDictionary(static p => p.Key, static p => p.Value);
    }

    /// <summary>
    /// Loads a collection into memory so that it can be searched or queried.
    /// </summary>
    /// <param name="replicaNumber">An optional replica number to load.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public async Task LoadAsync(int? replicaNumber = null, CancellationToken cancellationToken = default)
    {
        var request = new LoadCollectionRequest { CollectionName = Name };

        if (replicaNumber is not null)
        {
            Verify.GreaterThanOrEqualTo(replicaNumber.Value, 1);

            request.ReplicaNumber = replicaNumber.Value;
        }

        if (DatabaseName is not null)
        {
            request.DbName = DatabaseName;
        }

        await _client.InvokeAsync(_client.GrpcClient.LoadCollectionAsync, request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Release a collection loaded before
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        var request = new ReleaseCollectionRequest { CollectionName = Name };

        if (DatabaseName is not null)
        {
            request.DbName = DatabaseName;
        }

        await _client.InvokeAsync(_client.GrpcClient.ReleaseCollectionAsync, request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the loading progress for a collection, and optionally one or more of its partitions.
    /// </summary>
    /// <param name="partitionNames">
    /// An optional list of partition names for which to check the loading progress.
    /// </param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public async Task<long> GetLoadingProgressAsync(
        IEnumerable<string>? partitionNames = null,
        CancellationToken cancellationToken = default)
    {
        GetLoadingProgressRequest request = new() { CollectionName = Name };

        if (DatabaseName is not null)
        {
            request.DbName = DatabaseName;
        }

        if (partitionNames is not null)
        {
            request.PartitionNames.AddRange(partitionNames);
        }

        GetLoadingProgressResponse response =
            await _client.InvokeAsync(_client.GrpcClient.GetLoadingProgressAsync, request, static r => r.Status,
                    cancellationToken)
                .ConfigureAwait(false);

        return response.Progress;
    }

    /// <summary>
    /// Polls Milvus for loading progress of a collection until it is fully loaded.
    /// To perform a single progress check, use <see cref="GetLoadingProgressAsync" />.
    /// </summary>
    /// <param name="partitionNames">Partition names.</param>
    /// <param name="waitingInterval">Waiting interval. Defaults to 500 milliseconds.</param>
    /// <param name="timeout">Timeout.</param>
    /// <param name="progress">Provides information about the progress of the loading operation.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    /// <exception cref="TimeoutException">Time out.</exception>
    public async Task WaitForCollectionLoadAsync(
        IList<string>? partitionNames = null,
        TimeSpan? waitingInterval = null,
        TimeSpan? timeout = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        partitionNames ??= Array.Empty<string>();

        await Poll(
            async () =>
            {
                long progress = await GetLoadingProgressAsync(partitionNames, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return (progress == 100, progress);
            },
            $"Timeout when waiting for collection '{Name}' to load",
            waitingInterval, timeout, progress, cancellationToken).ConfigureAwait(false);
    }
}
