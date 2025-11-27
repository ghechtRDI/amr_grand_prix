using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmrGrandPrix.API.Models;

public class UploadBatch
{
    [Key]
    public Guid UploadBatchId { get; set; }

    [Required]
    public Guid RaceId { get; set; }

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

    // Navigation properties
    [ForeignKey("RaceId")]
    public virtual Race Race { get; set; } = null!;

    public virtual ICollection<RaceResult> Results { get; set; } = new List<RaceResult>();
}
