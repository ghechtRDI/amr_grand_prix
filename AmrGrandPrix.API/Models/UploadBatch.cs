using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmrGrandPrix.API.Models;

public class UploadBatch
{
    [Key]
    public Guid UploadBatchId { get; set; }

    /// <summary>
    /// The race these results belong to. Null only while a multi-variant upload is pending: its
    /// races are created per variant when the reviewed results are saved, so the batch is held
    /// against <see cref="RaceSeriesId"/> + <see cref="RaceDate"/> until then.
    /// </summary>
    public Guid? RaceId { get; set; }

    /// <summary>Series of a pending multi-variant upload (see <see cref="RaceId"/>).</summary>
    public Guid? RaceSeriesId { get; set; }

    /// <summary>Race date of a pending multi-variant upload (see <see cref="RaceId"/>).</summary>
    public DateOnly? RaceDate { get; set; }

    /// <summary>
    /// The variants the admin said a multi-variant upload's file contains, so resuming it routes
    /// sections to the same set. Empty for a single-race upload.
    /// </summary>
    public List<Guid> IncludedVariantIds { get; set; } = new();

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public FileType FileType { get; set; }

    /// <summary>
    /// Number of records successfully uploaded
    /// </summary>
    [Required]
    public int RecordsUploaded { get; set; }

    /// <summary>
    /// User ID of the person who uploaded this batch
    /// </summary>
    [Required]
    public string UploadedBy { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public UploadStatus Status { get; set; } = UploadStatus.Pending;

    // Background extraction inputs (see ExtractionWorker). The file's text is pulled out during
    // the upload request so the worker - including a restart re-queue - never needs the file.
    /// <summary>Plain text extracted from the uploaded file and sent to the LLM.</summary>
    public string? ExtractedText { get; set; }

    /// <summary>Course/variant names passed to the LLM as a labeling hint.</summary>
    public List<string> KnownVariants { get; set; } = new();

    /// <summary>The file covers exactly <see cref="KnownVariants"/> (multi-variant upload).</summary>
    public bool OnlyTheseVariants { get; set; }

    /// <summary>Rough count of result rows in <see cref="ExtractedText"/>, for the progress bar. Null if unknown.</summary>
    public int? ExpectedRows { get; set; }

    /// <summary>Why extraction failed, when <see cref="Status"/> is <see cref="UploadStatus.Failed"/>.</summary>
    [MaxLength(2000)]
    public string? ExtractionError { get; set; }

    // LLM audit fields
    public string? RawLlmJson     { get; set; }
    public string? LlmModel       { get; set; }
    public int     LlmInputTokens  { get; set; }
    public int     LlmOutputTokens { get; set; }

    // Navigation properties
    [ForeignKey("RaceId")]
    public virtual Race? Race { get; set; }

    [ForeignKey("RaceSeriesId")]
    public virtual RaceSeries? RaceSeries { get; set; }

    public virtual ICollection<RaceResult> Results { get; set; } = new List<RaceResult>();
}
