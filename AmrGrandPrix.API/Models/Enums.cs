namespace AmrGrandPrix.API.Models;

public enum Gender
{
    Male,
    Female,
    Nonbinary
}

public enum ResultStatus
{
    Finished,
    DNF,  // Did Not Finish
    DNS,  // Did Not Start
    DQ    // Disqualified
}

public enum Division
{
    OpenMale,
    OpenFemale,
    AgeMale,
    AgeFemale,
    OpenNonbinary,
    AgeNonbinary
}

public enum FileType
{
    CSV,
    Excel,
    Text,
    PDF
}

public enum UploadStatus
{
    /// <summary>Extraction finished; the results are ready for review in the upload wizard.</summary>
    Pending,
    Validated,
    Saved,
    Cancelled,
    /// <summary>Queued for, or running, LLM extraction in the background (<c>ExtractionWorker</c>).</summary>
    Extracting,
    /// <summary>LLM extraction failed; see <see cref="UploadBatch.ExtractionError"/>.</summary>
    Failed
}

public enum ClaimStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ReportStatus
{
    New,
    Reviewed
}
