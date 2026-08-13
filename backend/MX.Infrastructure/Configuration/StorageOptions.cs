namespace MX.Infrastructure.Configuration;

/// <summary>
/// Where the app keeps its data. Bound from the "Storage" configuration section,
/// so paths are settable per environment without a recompile — which is exactly
/// how the integration tests point the app at a throwaway copy of the dataset.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// The ticket store. Relative paths resolve against the app's content root.
    /// </summary>
    public string DataFilePath { get; set; } = Path.Combine("Data", "dataset.json");

    /// <summary>
    /// Where uploaded images are written. Under wwwroot so the static file
    /// middleware serves them back at <c>/uploads/{file}</c>.
    /// </summary>
    public string UploadsDirectory { get; set; } = Path.Combine("wwwroot", "uploads");

    /// <summary>
    /// Largest image accepted. Generous for a phone photo, small enough that a
    /// single upload cannot fill the disk or stall the request pipeline.
    /// </summary>
    public long MaxImageSizeBytes { get; set; } = 5 * 1024 * 1024;
}
