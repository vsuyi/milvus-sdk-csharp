﻿using IO.Milvus.Client.REST;
using IO.Milvus.Diagnostics;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace IO.Milvus.ApiSchema;

internal sealed class DropIndexRequest
{
    [JsonPropertyName("collection_name")]
    public string CollectionName { get; set; }

    [JsonPropertyName("field_name")]
    public string FieldName { get; set; }

    [JsonPropertyName("index_name")]
    public string IndexName { get; set; }

    /// <summary>
    /// Database name
    /// </summary>
    [JsonPropertyName("db_name")]
    public string DbName { get; set; }

    public static DropIndexRequest Create(
        string collectionName,
        string fieldName,
        string indexName,
        string dbName)
    {
        return new DropIndexRequest(collectionName, fieldName, indexName, dbName);
    }

    public Grpc.DropIndexRequest BuildGrpc()
    {
        this.Validate();

        var request = new Grpc.DropIndexRequest()
        {
            CollectionName = this.CollectionName,
            FieldName = this.FieldName,
            IndexName = this.IndexName,
            DbName = this.DbName
        };

        return request;
    }

    public HttpRequestMessage BuildRest()
    {
        this.Validate();

        return HttpRequest.CreateDeleteRequest(
            $"{ApiVersion.V1}/index",
            payload: this
            );
    }

    public void Validate()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(FieldName);
        Verify.NotNullOrWhiteSpace(IndexName);
        Verify.NotNullOrWhiteSpace(DbName);
    }

    #region Private ======================================================
    public DropIndexRequest(string collectionName, string fieldName, string indexName, string dbName)
    {
        this.CollectionName = collectionName;
        this.FieldName = fieldName;
        this.IndexName = indexName;
        this.DbName = dbName;
    }
    #endregion
}