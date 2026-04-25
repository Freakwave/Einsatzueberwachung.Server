namespace Einsatzueberwachung.Server.Training;

public sealed class TrainingApiOptions
{
    public const string SectionName = "TrainingApi";

    public bool Enabled { get; set; } = true;
    public bool AllowWriteOperations { get; set; } = false;
    public string ApiVersion { get; set; } = "v1";
    public List<string> AllowedOrigins { get; set; } = new();
    public string InstanceName { get; set; } = "Einsatzueberwachung.Server";
}
