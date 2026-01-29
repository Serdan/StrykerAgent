using System.Text.Json.Serialization;

namespace StrykerAgent;

public sealed class MutationTestReport
{
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }

    [JsonPropertyName("thresholds")]
    public ReportThresholds? Thresholds { get; set; }

    [JsonPropertyName("files")]
    public Dictionary<string, ReportFile>? Files { get; set; }

    [JsonPropertyName("testFiles")]
    public Dictionary<string, ReportTestFile>? TestFiles { get; set; }

    [JsonPropertyName("performance")]
    public ReportPerformance? Performance { get; set; }

    [JsonPropertyName("framework")]
    public ReportFramework? Framework { get; set; }

    [JsonPropertyName("system")]
    public ReportSystem? System { get; set; }
}

public sealed class ReportThresholds
{
    [JsonPropertyName("high")]
    public int? High { get; set; }

    [JsonPropertyName("low")]
    public int? Low { get; set; }
}

public sealed class ReportFile
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("mutants")]
    public List<ReportMutant>? Mutants { get; set; }
}

public sealed class ReportMutant
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mutatorName")]
    public string? MutatorName { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("location")]
    public ReportLocation? Location { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("replacement")]
    public string? Replacement { get; set; }

    [JsonPropertyName("statusReason")]
    public string? StatusReason { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("testsCompleted")]
    public double? TestsCompleted { get; set; }

    [JsonPropertyName("coveredBy")]
    public List<string>? CoveredBy { get; set; }

    [JsonPropertyName("killedBy")]
    public List<string>? KilledBy { get; set; }

    [JsonPropertyName("static")]
    public bool? Static { get; set; }
}

public sealed class ReportLocation
{
    [JsonPropertyName("start")]
    public ReportPosition? Start { get; set; }

    [JsonPropertyName("end")]
    public ReportPosition? End { get; set; }
}

public sealed class ReportPosition
{
    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("column")]
    public int? Column { get; set; }
}

public sealed class ReportTestFile
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("tests")]
    public List<ReportTest>? Tests { get; set; }
}

public sealed class ReportTest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class ReportPerformance
{
    [JsonPropertyName("setup")]
    public double? Setup { get; set; }

    [JsonPropertyName("initialRun")]
    public double? InitialRun { get; set; }

    [JsonPropertyName("mutation")]
    public double? Mutation { get; set; }
}

public sealed class ReportFramework
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("branding")]
    public ReportFrameworkBranding? Branding { get; set; }
}

public sealed class ReportFrameworkBranding
{
    [JsonPropertyName("homepageUrl")]
    public string? HomepageUrl { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}

public sealed class ReportSystem
{
    [JsonPropertyName("ci")]
    public bool? Ci { get; set; }

    [JsonPropertyName("os")]
    public ReportSystemOs? Os { get; set; }

    [JsonPropertyName("cpu")]
    public ReportSystemCpu? Cpu { get; set; }

    [JsonPropertyName("ram")]
    public ReportSystemRam? Ram { get; set; }
}

public sealed class ReportSystemOs
{
    [JsonPropertyName("platform")]
    public string? Platform { get; set; }
}

public sealed class ReportSystemCpu
{
    [JsonPropertyName("logicalCores")]
    public double? LogicalCores { get; set; }
}

public sealed class ReportSystemRam
{
    [JsonPropertyName("total")]
    public double? Total { get; set; }
}
