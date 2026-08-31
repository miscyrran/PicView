using PicView.Core.DebugTools;

namespace PicView.Core.Metadata;

/// <summary>
/// Finds Stable Diffusion generation parameters in an image and decodes them.
/// <para>
/// PNG keeps them in text chunks, which are read straight from the file. JPEG and WebP keep
/// them in the EXIF UserComment, which the caller has already decoded, or in the JPEG comment
/// segment.
/// </para>
/// </summary>
public static class SdMetadataReader
{
    /// <summary>
    /// Reads whatever generation parameters the image carries, or null when it has none.
    /// </summary>
    /// <param name="fileInfo">The image file. PNG text chunks are read from it directly.</param>
    /// <param name="exifUserComment">Decoded EXIF UserComment, where JPEG and WebP output lands.</param>
    /// <param name="imageComment">The image's comment attribute, e.g. the JPEG COM segment.</param>
    public static SdMetadata? Read(FileInfo? fileInfo, string? exifUserComment = null, string? imageComment = null)
    {
        try
        {
            if (fileInfo is { Exists: true })
            {
                using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var chunks = PngTextChunkReader.Read(stream);
                if (chunks is not null)
                {
                    var fromChunks = ReadFromPngChunks(chunks);
                    if (fromChunks is not null)
                    {
                        return fromChunks;
                    }
                }
            }

            return ReadFromText(exifUserComment) ?? ReadFromText(imageComment);
        }
        catch (Exception e)
        {
            DebugHelper.LogDebug(nameof(SdMetadataReader), nameof(Read), e);
            return null;
        }
    }

    private static SdMetadata? ReadFromPngChunks(Dictionary<string, string> chunks)
    {
        // Automatic1111, Forge, SD.Next and most of their forks.
        if (TryGet(chunks, "parameters", out var parameters))
        {
            var fromParameters = ParseUnknownPayload(parameters);
            if (fromParameters is not null)
            {
                return fromParameters;
            }
        }

        // InvokeAI.
        if (TryGet(chunks, "invokeai_metadata", out var invokeAi))
        {
            var parsed = SdMetadataParser.ParseInvokeAi(invokeAi);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        // NovelAI splits its metadata across Description and Comment.
        if (TryGet(chunks, "Software", out var software) &&
            software.Contains("NovelAI", StringComparison.OrdinalIgnoreCase))
        {
            chunks.TryGetValue("Description", out var description);
            chunks.TryGetValue("Comment", out var novelAiComment);
            var parsed = SdMetadataParser.ParseNovelAi(description, novelAiComment);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        // ComfyUI writes the API graph to "prompt" and the editor graph to "workflow".
        if (TryGet(chunks, "prompt", out var prompt))
        {
            chunks.TryGetValue("workflow", out var workflow);
            var parsed = SdMetadataParser.ParseComfyUi(prompt, workflow);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        // Older InvokeAI, and anything else that dropped a payload in a comment chunk.
        return (TryGet(chunks, "sd-metadata", out var sdMetadata) ? ParseUnknownPayload(sdMetadata) : null) ??
               (TryGet(chunks, "Comment", out var comment) ? ParseUnknownPayload(comment) : null);
    }

    /// <summary>
    /// Decodes a payload whose producer is not known from the chunk name it arrived under.
    /// </summary>
    private static SdMetadata? ParseUnknownPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        if (payload.TrimStart().StartsWith('{'))
        {
            return SdMetadataParser.ParseInvokeAi(payload) ?? SdMetadataParser.ParseComfyUi(payload, null);
        }

        return SdMetadataParser.ParseA1111(payload);
    }

    /// <summary>
    /// Decodes an EXIF or JPEG comment. Unlike a PNG chunk these are not SD-specific, so a
    /// payload only counts when it actually looks like generation parameters — otherwise every
    /// camera comment would show up as a prompt.
    /// </summary>
    private static SdMetadata? ReadFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.TrimStart();

        if (trimmed.StartsWith('{'))
        {
            return SdMetadataParser.ParseInvokeAi(text) ?? SdMetadataParser.ParseComfyUi(text, null);
        }

        if (!text.Contains("Steps:", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("Negative prompt:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return SdMetadataParser.ParseA1111(text);
    }

    private static bool TryGet(Dictionary<string, string> chunks, string key, out string value)
    {
        if (chunks.TryGetValue(key, out var found) && !string.IsNullOrWhiteSpace(found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
