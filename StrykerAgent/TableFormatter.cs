namespace StrykerAgent;

public static class TableFormatter
{
    public static void WriteFiles(IEnumerable<FileSummary> files)
    {
        var rows = files.Select(file => new[]
        {
            file.FilePath,
            file.Language,
            file.MutationScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            file.TotalMutants.ToString(System.Globalization.CultureInfo.InvariantCulture),
            file.StatusCounts.Survived.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).ToList();

        WriteTable(["Path", "Lang", "Score", "Mutants", "Survived"], rows);
    }

    public static void WriteMutants(IEnumerable<MutantRecord> mutants)
    {
        var rows = mutants.Select(mutant => new[]
        {
            mutant.Id,
            mutant.FilePath,
            $"{mutant.Location.StartLine}:{mutant.Location.StartColumn}",
            mutant.Mutator,
            mutant.Status
        }).ToList();

        WriteTable(["Id", "File", "Loc", "Mutator", "Status"], rows);
    }

    private static void WriteTable(string[] headers, List<string[]> rows)
    {
        var widths = new int[headers.Length];
        for (var i = 0; i < headers.Length; i++)
        {
            widths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        Console.WriteLine(FormatRow(headers, widths));
        Console.WriteLine(FormatRow(headers.Select((_, index) => new string('-', widths[index])).ToArray(), widths));

        foreach (var row in rows)
        {
            Console.WriteLine(FormatRow(row, widths));
        }
    }

    private static string FormatRow(string[] columns, int[] widths)
    {
        var padded = new string[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            padded[i] = columns[i].PadRight(widths[i]);
        }

        return string.Join("  ", padded);
    }
}
