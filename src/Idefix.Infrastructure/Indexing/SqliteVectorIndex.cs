using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using Idefix.Core.Indexing;
using Idefix.Core.Knowledge;
using Idefix.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Idefix.Infrastructure.Indexing;

public sealed class SqliteVectorIndex : IVectorIndex, IDisposable
{
    private const string ChunkColumns = "id, document_key, plugin, file_name, page, first_line, last_line, text";

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS documents (
            key TEXT PRIMARY KEY,
            plugin TEXT NOT NULL,
            length INTEGER NOT NULL,
            last_write_ticks INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS chunks (
            id TEXT PRIMARY KEY,
            document_key TEXT NOT NULL REFERENCES documents(key) ON DELETE CASCADE,
            plugin TEXT NOT NULL,
            file_name TEXT NOT NULL,
            page INTEGER NOT NULL,
            first_line INTEGER NOT NULL,
            last_line INTEGER NOT NULL,
            text TEXT NOT NULL,
            vector BLOB NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_chunks_plugin ON chunks(plugin);
        CREATE INDEX IF NOT EXISTS ix_chunks_document ON chunks(document_key);
        """;

    private static readonly string[] ChunkParameterNames = ["$id", "$doc", "$plugin", "$file", "$page", "$first", "$last", "$text", "$vector"];

    private readonly string connectionString;
    private readonly SemaphoreSlim initialization = new(1, 1);
    private readonly string embeddingModel;
    private bool initialized;

    public SqliteVectorIndex(string rootDirectory, string embeddingModel)
    {
        var directory = KnowledgeLayout.IndexDirectory(rootDirectory);
        var info = Directory.CreateDirectory(directory);
        info.Attributes |= FileAttributes.Hidden;
        this.DatabasePath = Path.Combine(directory, "index.db");
        this.connectionString = new SqliteConnectionStringBuilder { DataSource = this.DatabasePath, Pooling = false }.ToString();
        this.embeddingModel = embeddingModel;
    }

    public string DatabasePath { get; }

    public async Task<IReadOnlyDictionary<string, DocumentFingerprint>> GetFingerprintsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await this.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, length, last_write_ticks FROM documents";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<string, DocumentFingerprint>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = new DocumentFingerprint(reader.GetInt64(1), new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero));
        }

        return result;
    }

    public async Task UpsertDocumentAsync(KnowledgeDocument document, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken)
    {
        await using var connection = await this.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(connection, transaction, "DELETE FROM documents WHERE key = $key", cancellationToken, ("$key", document.Key));
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO documents (key, plugin, length, last_write_ticks) VALUES ($key, $plugin, $length, $ticks)",
            cancellationToken,
            ("$key", document.Key),
            ("$plugin", document.PluginName),
            ("$length", document.Length),
            ("$ticks", document.LastWriteUtc.UtcTicks));

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT OR REPLACE INTO chunks (id, document_key, plugin, file_name, page, first_line, last_line, text, vector)
            VALUES ($id, $doc, $plugin, $file, $page, $first, $last, $text, $vector)
            """;
        var parameters = ChunkParameterNames
            .Select(name => insert.Parameters.Add(new SqliteParameter { ParameterName = name }))
            .ToArray();

        foreach (var embedded in chunks)
        {
            var chunk = embedded.Chunk;
            parameters[0].Value = chunk.Id;
            parameters[1].Value = chunk.DocumentKey;
            parameters[2].Value = chunk.PluginName;
            parameters[3].Value = chunk.FileName;
            parameters[4].Value = chunk.Page;
            parameters[5].Value = chunk.FirstLine;
            parameters[6].Value = chunk.LastLine;
            parameters[7].Value = chunk.Text;
            parameters[8].Value = MemoryMarshal.AsBytes(embedded.Vector.Span).ToArray();
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RemoveDocumentAsync(string documentKey, CancellationToken cancellationToken)
    {
        await using var connection = await this.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, transaction: null, "DELETE FROM documents WHERE key = $key", cancellationToken, ("$key", documentKey));
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(ReadOnlyMemory<float> query, IReadOnlyCollection<string> plugins, int top, CancellationToken cancellationToken)
    {
        if (plugins.Count == 0 || top <= 0)
        {
            return [];
        }

        await using var connection = await this.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ChunkColumns}, vector FROM chunks WHERE plugin IN (SELECT value FROM json_each($plugins))";
        command.Parameters.AddWithValue("$plugins", JsonSerializer.Serialize(plugins, IdefixJsonContext.Default.IReadOnlyCollectionString));

        var best = new PriorityQueue<ScoredChunk, double>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var vector = MemoryMarshal.Cast<byte, float>((byte[])reader.GetValue(8));
            var score = VectorMath.CosineSimilarity(query.Span, vector);
            if (best.Count < top)
            {
                best.Enqueue(new ScoredChunk(ReadChunk(reader), score), score);
            }
            else if (best.TryPeek(out _, out var lowest) && score > lowest)
            {
                best.EnqueueDequeue(new ScoredChunk(ReadChunk(reader), score), score);
            }
        }

        var results = new List<ScoredChunk>(best.Count);
        while (best.TryDequeue(out var item, out _))
        {
            results.Add(item);
        }

        results.Reverse();
        return results;
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(string documentKey, CancellationToken cancellationToken)
    {
        await using var connection = await this.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ChunkColumns} FROM chunks WHERE document_key = $key ORDER BY page, first_line";
        command.Parameters.AddWithValue("$key", documentKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var chunks = new List<DocumentChunk>();
        while (await reader.ReadAsync(cancellationToken))
        {
            chunks.Add(ReadChunk(reader));
        }

        return chunks;
    }

    public void Dispose() => this.initialization.Dispose();

    private static DocumentChunk ReadChunk(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        reader.GetInt32(5),
        reader.GetInt32(6),
        reader.GetString(7));

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Callers pass literal SQL only; values travel as parameters.")]
    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(this.connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await ExecuteAsync(connection, transaction: null, "PRAGMA foreign_keys = ON", cancellationToken);
            await this.EnsureInitializedAsync(connection, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task EnsureInitializedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (this.initialized)
        {
            return;
        }

        await this.initialization.WaitAsync(cancellationToken);
        try
        {
            if (this.initialized)
            {
                return;
            }

            await ExecuteAsync(connection, transaction: null, "PRAGMA journal_mode = WAL", cancellationToken);
            await ExecuteAsync(connection, transaction: null, Schema, cancellationToken);

            await using var read = connection.CreateCommand();
            read.CommandText = "SELECT value FROM meta WHERE key = 'embedding_model'";
            var stored = await read.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(stored, this.embeddingModel, StringComparison.Ordinal))
            {
                // Vectors from different embedding models are not comparable, so a model switch forces a full rebuild.
                await ExecuteAsync(connection, transaction: null, "DELETE FROM chunks; DELETE FROM documents;", cancellationToken);
                await ExecuteAsync(
                    connection,
                    transaction: null,
                    "INSERT OR REPLACE INTO meta (key, value) VALUES ('embedding_model', $model)",
                    cancellationToken,
                    ("$model", this.embeddingModel));
            }

            this.initialized = true;
        }
        finally
        {
            this.initialization.Release();
        }
    }
}
