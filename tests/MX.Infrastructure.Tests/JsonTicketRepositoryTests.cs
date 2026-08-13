using MX.Domain.Tickets;
using MX.Infrastructure.Persistence;

namespace MX.Infrastructure.Tests;

/// <summary>
/// Exercises the repository against a scratch copy of the real dataset.json.
/// Each test gets its own temp file, so they neither interfere with one another
/// nor touch the file committed to the repo.
/// </summary>
public sealed class JsonTicketRepositoryTests : IDisposable
{
    private static readonly string SourceDataset =
        Path.Combine(AppContext.BaseDirectory, "TestData", "dataset.json");

    private readonly string _workingCopy;

    public JsonTicketRepositoryTests()
    {
        _workingCopy = Path.Combine(Path.GetTempPath(), $"mx-tickets-{Guid.NewGuid():N}.json");
        File.Copy(SourceDataset, _workingCopy);
    }

    public void Dispose()
    {
        if (File.Exists(_workingCopy))
        {
            File.Delete(_workingCopy);
        }
    }

    private JsonTicketRepository CreateRepository() => new(_workingCopy);

    [Fact]
    public async Task Loads_every_ticket_from_the_supplied_dataset()
    {
        using var repository = CreateRepository();

        var tickets = await repository.GetAllAsync();

        Assert.Equal(5, tickets.Count);
        Assert.All(tickets, t => Assert.NotEqual(Guid.Empty, t.Id));
    }

    [Fact]
    public async Task Parses_the_status_containing_a_space()
    {
        // The regression this whole converter exists for: "In Progress" is not a
        // valid C# identifier, so the default enum handling cannot read it.
        using var repository = CreateRepository();

        var tickets = await repository.GetAllAsync();

        Assert.Contains(tickets, t => t.Status == TicketStatus.InProgress);
        Assert.Contains(tickets, t => t.Status == TicketStatus.New);
        Assert.Contains(tickets, t => t.Status == TicketStatus.Closed);
        Assert.Contains(tickets, t => t.Status == TicketStatus.Resolved);
    }

    [Fact]
    public async Task Rewriting_an_unchanged_dataset_reproduces_the_original_file()
    {
        // The strongest guarantee available: load everything, save it back, and
        // require the bytes to be identical. This pins the status spelling, the
        // timestamp format, property order, indentation, and character escaping
        // all at once.
        var original = await File.ReadAllTextAsync(_workingCopy);

        using var repository = CreateRepository();
        var tickets = await repository.GetAllAsync();
        await repository.UpdateAsync(tickets[0]);

        var rewritten = await File.ReadAllTextAsync(_workingCopy);

        Assert.Equal(original, rewritten);
    }

    [Fact]
    public async Task Preserves_the_non_ascii_apostrophe_in_the_dataset()
    {
        // U+2019 in "doesn't". Default escaping would rewrite it as ’ —
        // valid JSON, but a needless diff on every save.
        using var repository = CreateRepository();
        var tickets = await repository.GetAllAsync();
        await repository.UpdateAsync(tickets[0]);

        var rewritten = await File.ReadAllTextAsync(_workingCopy);

        Assert.Contains('’', rewritten);
        Assert.DoesNotContain("\\u2019", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Round_trips_a_ticket_through_disk_with_its_values_intact()
    {
        using var writer = CreateRepository();
        var created = Ticket.Create("Ada Lovelace", "ada@example.com", "The printer is on fire.", "Printer ablaze.");
        await writer.AddAsync(created);

        // A second instance is forced to read from disk rather than the cache.
        using var reader = CreateRepository();
        var loaded = await reader.GetByIdAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal(created.Name, loaded.Name);
        Assert.Equal(created.Email, loaded.Email);
        Assert.Equal(created.Description, loaded.Description);
        Assert.Equal(created.Summary, loaded.Summary);
        Assert.Equal(TicketStatus.New, loaded.Status);
        Assert.Null(loaded.Resolution);
        Assert.Equal(created.CreatedAt.ToUnixTimeSeconds(), loaded.CreatedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Update_persists_a_status_change()
    {
        using var repository = CreateRepository();
        var tickets = await repository.GetAllAsync();
        var target = tickets.First(t => t.Status != TicketStatus.Closed);

        target.ChangeStatus(TicketStatus.Closed);
        await repository.UpdateAsync(target);

        using var reloaded = CreateRepository();
        var persisted = await reloaded.GetByIdAsync(target.Id);

        Assert.Equal(TicketStatus.Closed, persisted!.Status);
    }

    [Fact]
    public async Task Rejects_adding_a_duplicate_id()
    {
        using var repository = CreateRepository();
        var existing = (await repository.GetAllAsync())[0];

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(existing));
    }

    [Fact]
    public async Task Rejects_updating_a_ticket_that_was_never_stored()
    {
        using var repository = CreateRepository();
        var stranger = Ticket.Create("Nobody", "nobody@example.com", "Never saved.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(stranger));
    }

    [Fact]
    public async Task Treats_a_missing_file_as_an_empty_store()
    {
        var absent = Path.Combine(Path.GetTempPath(), $"mx-missing-{Guid.NewGuid():N}.json");
        using var repository = new JsonTicketRepository(absent);

        var tickets = await repository.GetAllAsync();

        Assert.Empty(tickets);
        Assert.False(File.Exists(absent));
    }

    [Fact]
    public async Task Creates_the_file_on_first_write_when_it_did_not_exist()
    {
        var absent = Path.Combine(Path.GetTempPath(), $"mx-new-{Guid.NewGuid():N}.json");
        try
        {
            using var repository = new JsonTicketRepository(absent);
            await repository.AddAsync(Ticket.Create("First", "first@example.com", "Brand new store."));

            Assert.True(File.Exists(absent));
            Assert.Single(await repository.GetAllAsync());
        }
        finally
        {
            if (File.Exists(absent))
            {
                File.Delete(absent);
            }
        }
    }

    [Fact]
    public async Task Concurrent_writes_all_survive()
    {
        // Twenty parallel adds through one instance. Without the semaphore the
        // last writer would win and the file would be missing tickets; with it,
        // every ticket must be present and the file must still be valid JSON.
        using var repository = CreateRepository();

        var additions = Enumerable.Range(0, 20).Select(i =>
            repository.AddAsync(Ticket.Create($"Customer {i}", $"c{i}@example.com", $"Issue number {i}.")));

        await Task.WhenAll(additions);

        using var reloaded = CreateRepository();
        var persisted = await reloaded.GetAllAsync();

        Assert.Equal(25, persisted.Count); // 5 seeded + 20 added
    }
}
