using System.Text.Encodings.Web;
using System.Text.Json;
using MX.Application.Abstractions;
using MX.Domain.Tickets;

namespace MX.Infrastructure.Persistence;

/// <summary>
/// Stores tickets in a JSON file, as the exercise requires.
///
/// Reads are served from an in-memory cache loaded once on first use; writes
/// re-serialize the whole collection. Both are serialized through a semaphore,
/// and the file is replaced atomically, so a crash or a concurrent request can
/// never leave a half-written dataset on disk.
///
/// Registered as a singleton — the cache and the lock only protect anything if
/// every request shares one instance.
/// </summary>
public sealed class JsonTicketRepository : ITicketRepository, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,

        // Keeps characters such as the U+2019 apostrophe in the supplied dataset
        // literal instead of rewriting them as ’ escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

        Converters = { new Iso8601UtcJsonConverter() }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<Ticket>? _cache;

    public JsonTicketRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tickets = await LoadUnsynchronisedAsync(cancellationToken).ConfigureAwait(false);

            // A copy of the list, not of the entities: callers must not be able to
            // add or remove tickets by mutating what a read handed them, but they
            // do need the live entity to record changes on.
            return tickets.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tickets = await LoadUnsynchronisedAsync(cancellationToken).ConfigureAwait(false);
            return tickets.SingleOrDefault(t => t.Id == id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tickets = await LoadUnsynchronisedAsync(cancellationToken).ConfigureAwait(false);

            if (tickets.Any(t => t.Id == ticket.Id))
            {
                throw new InvalidOperationException($"A ticket with id {ticket.Id} already exists.");
            }

            tickets.Add(ticket);
            await SaveUnsynchronisedAsync(tickets, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tickets = await LoadUnsynchronisedAsync(cancellationToken).ConfigureAwait(false);

            var index = tickets.FindIndex(t => t.Id == ticket.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"No ticket with id {ticket.Id} exists.");
            }

            // The caller may hold a different instance than the cache does
            // (for example after a round trip through a mapper), so replace rather
            // than assume reference equality.
            tickets[index] = ticket;
            await SaveUnsynchronisedAsync(tickets, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Loads the file on first use. Callers must already hold <see cref="_gate"/> —
    /// hence the name, which makes an unguarded call read as a mistake.
    /// </summary>
    private async Task<List<Ticket>> LoadUnsynchronisedAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            // A missing store is an empty store; the first write creates the file.
            return _cache = [];
        }

        await using var stream = File.OpenRead(_filePath);

        var records = await JsonSerializer
            .DeserializeAsync<List<TicketRecord>>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return _cache = records?.Select(r => r.ToDomain()).ToList() ?? [];
    }

    /// <summary>
    /// Writes the whole collection, then swaps it into place. Callers must already
    /// hold <see cref="_gate"/>.
    /// </summary>
    private async Task SaveUnsynchronisedAsync(List<Ticket> tickets, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var records = tickets.Select(TicketRecord.FromDomain).ToList();
        var json = JsonSerializer.Serialize(records, SerializerOptions);

        // Write beside the target so the move below stays on one volume, which is
        // what makes it atomic. A torn write can only ever damage the temp file.
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _filePath, overwrite: true);

        _cache = tickets;
    }

    public void Dispose() => _gate.Dispose();
}
