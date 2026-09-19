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
    AgeFemale
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
    Pending,
    Validated,
    Saved,
    Cancelled
}

public enum ClaimStatus
{
    Pending,
    Approved,
    Rejected
}
