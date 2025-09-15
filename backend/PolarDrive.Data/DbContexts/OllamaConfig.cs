namespace PolarDrive.Data.DbContexts;

public class OllamaConfig
{
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public float Temperature { get; set; }
    public float TopP { get; set; }
    public int ContextWindow { get; set; }
    public int MaxTokens { get; set; }
    public float RepeatPenalty { get; set; }
    public int TopK { get; set; }
    public int MaxRetries { get; set; }
    public int RetryDelaySeconds { get; set; }
}
