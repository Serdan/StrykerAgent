using System.Text.Json.Serialization;

namespace StrykerAgent;

public sealed class MutantRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("location")]
    public MutantLocation Location { get; set; } = new();

    [JsonPropertyName("mutator")]
    public string Mutator { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("replacement")]
    public string? Replacement { get; set; }

    [JsonPropertyName("statusReason")]
    public string? StatusReason { get; set; }

    [JsonPropertyName("durationMs")]
    public double? DurationMs { get; set; }

    [JsonPropertyName("testsCompleted")]
    public double? TestsCompleted { get; set; }

    [JsonPropertyName("coveredBy")]
    public List<TestRef> CoveredBy { get; set; } = [];

    [JsonPropertyName("killedBy")]
    public List<TestRef> KilledBy { get; set; } = [];

    [JsonPropertyName("isStatic")]
    public bool? IsStatic { get; set; }
}

public sealed class MutantLocation
{
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    [JsonPropertyName("startColumn")]
    public int StartColumn { get; set; }

    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    [JsonPropertyName("endColumn")]
    public int EndColumn { get; set; }
}

public sealed class TestRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class FileSummary
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("mutationScore")]
    public double MutationScore { get; set; }

    [JsonPropertyName("statusCounts")]
    public StatusCounts StatusCounts { get; set; } = new();

    [JsonPropertyName("totalMutants")]
    public int TotalMutants { get; set; }
}

public sealed class StatusCounts
{
    [JsonPropertyName("Killed")]
    public int Killed { get; set; }

    [JsonPropertyName("Survived")]
    public int Survived { get; set; }

    [JsonPropertyName("NoCoverage")]
    public int NoCoverage { get; set; }

    [JsonPropertyName("CompileError")]
    public int CompileError { get; set; }

    [JsonPropertyName("RuntimeError")]
    public int RuntimeError { get; set; }

    [JsonPropertyName("Timeout")]
    public int Timeout { get; set; }

    [JsonPropertyName("Ignored")]
    public int Ignored { get; set; }

    [JsonPropertyName("Pending")]
    public int Pending { get; set; }

    public void Increment(string status)
    {
        switch (status)
        {
            case "Killed":
                Killed++;
                break;
            case "Survived":
                Survived++;
                break;
            case "NoCoverage":
                NoCoverage++;
                break;
            case "CompileError":
                CompileError++;
                break;
            case "RuntimeError":
                RuntimeError++;
                break;
            case "Timeout":
                Timeout++;
                break;
            case "Ignored":
                Ignored++;
                break;
            case "Pending":
                Pending++;
                break;
            default:
                throw new AgentException(ExitCodes.ReportParseError, $"Unknown status: {status}");
        }
    }

    public void Add(StatusCounts other)
    {
        Killed += other.Killed;
        Survived += other.Survived;
        NoCoverage += other.NoCoverage;
        CompileError += other.CompileError;
        RuntimeError += other.RuntimeError;
        Timeout += other.Timeout;
        Ignored += other.Ignored;
        Pending += other.Pending;
    }
}

public sealed class RunSummary
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "";

    [JsonPropertyName("thresholds")]
    public RunThresholds Thresholds { get; set; } = new();

    [JsonPropertyName("totals")]
    public RunTotals Totals { get; set; } = new();

    [JsonPropertyName("statusCounts")]
    public StatusCounts StatusCounts { get; set; } = new();

    [JsonPropertyName("mutationScore")]
    public MutationScore MutationScore { get; set; } = new();

    [JsonPropertyName("performanceMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PerformanceMs? PerformanceMs { get; set; }

    [JsonPropertyName("framework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FrameworkInfo? Framework { get; set; }

    [JsonPropertyName("system")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SystemInfo? System { get; set; }
}

public sealed class RunThresholds
{
    [JsonPropertyName("high")]
    public int High { get; set; }

    [JsonPropertyName("low")]
    public int Low { get; set; }
}

public sealed class RunTotals
{
    [JsonPropertyName("files")]
    public int Files { get; set; }

    [JsonPropertyName("mutants")]
    public int Mutants { get; set; }
}

public sealed class MutationScore
{
    [JsonPropertyName("percent")]
    public double Percent { get; set; }

    [JsonPropertyName("numeratorKilled")]
    public int NumeratorKilled { get; set; }

    [JsonPropertyName("denominatorEffective")]
    public int DenominatorEffective { get; set; }
}

public sealed class PerformanceMs
{
    [JsonPropertyName("setup")]
    public double Setup { get; set; }

    [JsonPropertyName("initialRun")]
    public double InitialRun { get; set; }

    [JsonPropertyName("mutation")]
    public double Mutation { get; set; }
}

public sealed class FrameworkInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class SystemInfo
{
    [JsonPropertyName("ci")]
    public bool Ci { get; set; }
}

public sealed class MutantGetResponse
{
    [JsonPropertyName("mutant")]
    public MutantRecord Mutant { get; set; } = new();

    [JsonPropertyName("context")]
    public MutantContext Context { get; set; } = new();
}

public sealed class MutantContext
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("mutantLocation")]
    public MutantLocation MutantLocation { get; set; } = new();
}
