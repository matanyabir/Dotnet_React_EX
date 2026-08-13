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
    /// Where uploaded images are written (Stage 8). Under wwwroot so the static
    /// file middleware can serve them back.
    /// </summary>
    public string UploadsDirectory { get; set; } = Path.Combine("wwwroot", "uploads");
}
