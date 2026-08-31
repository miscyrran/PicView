namespace PicView.Core.Metadata;

/// <summary>
/// Generation parameters recovered from an AI-generated image.
/// </summary>
public sealed class SdMetadata
{
    /// <summary>Tool that wrote the metadata, e.g. "Automatic1111" or "ComfyUI".</summary>
    public required string Generator { get; init; }

    /// <summary>Positive prompt, when one could be isolated.</summary>
    public string? Prompt { get; init; }

    /// <summary>Negative prompt, when one could be isolated.</summary>
    public string? NegativePrompt { get; init; }

    /// <summary>Sampler, steps, CFG, seed, model and friends, formatted for display.</summary>
    public string? Settings { get; init; }

    /// <summary>The unmodified payload, so nothing is lost when parsing falls short.</summary>
    public required string Raw { get; init; }
}
