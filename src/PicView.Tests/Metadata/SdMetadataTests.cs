using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using PicView.Core.Metadata;

namespace PicView.Tests.Metadata;

public class SdMetadataTests
{
    private const string A1111Payload =
        "a cat wearing a hat, masterpiece, best quality\n" +
        "Negative prompt: blurry, worst quality\n" +
        "Steps: 25, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 1234567890, Size: 512x768, Model: someModel";

    [Fact]
    public void ParseA1111_FullPayload_SplitsPromptNegativeAndSettings()
    {
        var metadata = SdMetadataParser.ParseA1111(A1111Payload);

        Assert.Equal("Automatic1111", metadata.Generator);
        Assert.Equal("a cat wearing a hat, masterpiece, best quality", metadata.Prompt);
        Assert.Equal("blurry, worst quality", metadata.NegativePrompt);
        Assert.StartsWith("Steps: 25, Sampler: DPM++ 2M Karras", metadata.Settings);
        Assert.Equal(A1111Payload, metadata.Raw);
    }

    [Fact]
    public void ParseA1111_WithoutNegativePrompt_LeavesNegativeNull()
    {
        var metadata = SdMetadataParser.ParseA1111("a cat\nSteps: 20, Sampler: Euler a");

        Assert.Equal("a cat", metadata.Prompt);
        Assert.Null(metadata.NegativePrompt);
        Assert.Equal("Steps: 20, Sampler: Euler a", metadata.Settings);
    }

    [Fact]
    public void ParseA1111_MultiLinePrompt_KeepsEveryPromptLine()
    {
        var metadata = SdMetadataParser.ParseA1111(
            "line one, <lora:something:0.8>\nline two\nNegative prompt: bad\nSteps: 10");

        Assert.Equal("line one, <lora:something:0.8>\nline two", metadata.Prompt);
        Assert.Equal("bad", metadata.NegativePrompt);
    }

    [Fact]
    public void ParseA1111_PromptOnly_TreatsEverythingAsPrompt()
    {
        // Prompt weighting uses colons and commas, so it must not be mistaken for settings.
        var metadata = SdMetadataParser.ParseA1111("(a cat:1.2), (a hat:0.8), detailed");

        Assert.Equal("(a cat:1.2), (a hat:0.8), detailed", metadata.Prompt);
        Assert.Null(metadata.Settings);
    }

    [Fact]
    public void ParseComfyUi_FollowsSamplerLinksToTextEncoders()
    {
        const string graph =
            """
            {
              "3": {
                "class_type": "KSampler",
                "inputs": {
                  "seed": 42, "steps": 30, "cfg": 8.0,
                  "sampler_name": "euler", "scheduler": "normal",
                  "positive": ["6", 0], "negative": ["7", 0]
                }
              },
              "4": { "class_type": "CheckpointLoaderSimple", "inputs": { "ckpt_name": "sd_xl_base.safetensors" } },
              "6": { "class_type": "CLIPTextEncode", "inputs": { "text": "a scenic landscape" } },
              "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "text, watermark" } }
            }
            """;

        var metadata = SdMetadataParser.ParseComfyUi(graph, null);

        Assert.NotNull(metadata);
        Assert.Equal("ComfyUI", metadata.Generator);
        Assert.Equal("a scenic landscape", metadata.Prompt);
        Assert.Equal("text, watermark", metadata.NegativePrompt);
        Assert.Contains("Steps: 30", metadata.Settings);
        Assert.Contains("Model: sd_xl_base.safetensors", metadata.Settings);
    }

    [Fact]
    public void ParseComfyUi_UnrelatedJson_ReturnsNull()
    {
        Assert.Null(SdMetadataParser.ParseComfyUi("""{ "hello": "world" }""", null));
    }

    [Fact]
    public void ParseNovelAi_ReadsPromptFromDescriptionAndNegativeFromComment()
    {
        var metadata = SdMetadataParser.ParseNovelAi(
            "1girl, forest",
            """{ "uc": "lowres, bad anatomy", "steps": 28, "scale": 11, "sampler": "k_euler_ancestral", "seed": 7 }""");

        Assert.NotNull(metadata);
        Assert.Equal("NovelAI", metadata.Generator);
        Assert.Equal("1girl, forest", metadata.Prompt);
        Assert.Equal("lowres, bad anatomy", metadata.NegativePrompt);
        Assert.Contains("Steps: 28", metadata.Settings);
        Assert.Contains("Seed: 7", metadata.Settings);
    }

    [Fact]
    public void Read_PngWithParametersChunk_ReturnsA1111Metadata()
    {
        var path = WritePng(("parameters", A1111Payload, false));
        try
        {
            var metadata = SdMetadataReader.Read(new FileInfo(path));

            Assert.NotNull(metadata);
            Assert.Equal("Automatic1111", metadata.Generator);
            Assert.Equal("a cat wearing a hat, masterpiece, best quality", metadata.Prompt);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_PngWithCompressedChunk_InflatesPayload()
    {
        var path = WritePng(("parameters", A1111Payload, true));
        try
        {
            var metadata = SdMetadataReader.Read(new FileInfo(path));

            Assert.NotNull(metadata);
            Assert.Equal("blurry, worst quality", metadata.NegativePrompt);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_PngWithoutTextChunks_ReturnsNull()
    {
        var path = WritePng();
        try
        {
            Assert.Null(SdMetadataReader.Read(new FileInfo(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ExifUserCommentWithParameters_ReturnsA1111Metadata()
    {
        var metadata = SdMetadataReader.Read(fileInfo: null, exifUserComment: A1111Payload);

        Assert.NotNull(metadata);
        Assert.Equal("blurry, worst quality", metadata.NegativePrompt);
    }

    [Fact]
    public void Read_OrdinaryComment_IsNotMistakenForGenerationParameters()
    {
        Assert.Null(SdMetadataReader.Read(fileInfo: null, exifUserComment: "Holiday photo, taken at the beach"));
    }

    /// <summary>
    /// Writes a PNG containing only a signature, an IHDR, the requested text chunks and an IEND.
    /// It is not a decodable image, which is the point: the reader must walk chunks without one.
    /// </summary>
    private static string WritePng(params (string Keyword, string Text, bool Compressed)[] textChunks)
    {
        var path = Path.Combine(Path.GetTempPath(), $"picview_sd_{Guid.NewGuid():N}.png");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);

        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "IHDR", new byte[13]);

        foreach (var (keyword, text, compressed) in textChunks)
        {
            var payload = new MemoryStream();
            payload.Write(Encoding.Latin1.GetBytes(keyword));
            payload.WriteByte(0);

            if (compressed)
            {
                payload.WriteByte(0); // compression method
                using (var zlib = new ZLibStream(payload, CompressionLevel.Optimal, leaveOpen: true))
                {
                    zlib.Write(Encoding.Latin1.GetBytes(text));
                }
            }
            else
            {
                payload.Write(Encoding.Latin1.GetBytes(text));
            }

            WriteChunk(stream, compressed ? "zTXt" : "tEXt", payload.ToArray());
        }

        WriteChunk(stream, "IEND", []);
        return path;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write(new byte[4]); // CRC, not verified by the reader
    }
}
