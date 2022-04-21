using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CommandLine;
using CsvHelper;

namespace CsvFileSplitter;

public class Program
{
    private const string OutputFileNameGroupReplacementString = "{group}";

    internal class Options
    {
        [Option('i', "input", Required = true, HelpText = "Path for input file.")]
        public string InputFilePath { get; set; } = null!;

        [Option('g', "group", Required = true,
            HelpText = "Regex group to select and split input file by. Format 'InputColumnName:RegexExpression'.")]
        public string Group { get; set; } = null!;

        [Option('r', "rowPerOutputFile", Required = false, Default = false,
            HelpText =
                "Specified whether one csv row can be mapped to multiple output files based on regex matched groups. Default is false.")]
        public bool RowPerOutputFile { get; set; }

        [Option('o', "output", Required = true,
            HelpText = $"Path for output files. File name must contain '{OutputFileNameGroupReplacementString}'.")]
        public string OutputFilePath { get; set; } = null!;
    }

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(SplitCsvFile)
            .WithNotParsed(_ => { Console.WriteLine("Failed to parse arguments."); });
    }

    private static void SplitCsvFile(Options opts)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        if (string.IsNullOrWhiteSpace(opts.Group))
        {
            Console.WriteLine($"Invalid group argument: '{opts.Group}'.");
            Console.WriteLine("Exiting...");
            return;
        }

        int indexOfSemicolon = opts.Group.IndexOf(":", StringComparison.InvariantCulture);
        if (indexOfSemicolon == -1)
        {
            Console.WriteLine($"Invalid group argument: '{opts.Group}'. Missing ':'.");
            Console.WriteLine("Exiting...");
            return;
        }

        if (!opts.OutputFilePath.Contains(OutputFileNameGroupReplacementString))
        {
            Console.WriteLine(
                $"Invalid OutputFilePath argument: '{opts.OutputFilePath}'. Path must contain '{OutputFileNameGroupReplacementString}'.");
            Console.WriteLine("Exiting...");
            return;
        }

        string columnsToGroupBy = opts.Group[..indexOfSemicolon];
        string regexToUse = opts.Group[++indexOfSemicolon..];
        Regex regex = new(regexToUse);


        using StreamReader reader = new(opts.InputFilePath);
        using CsvReader csvReader = new(reader, CultureInfo.InvariantCulture);
        csvReader.Read();
        csvReader.ReadHeader();

        Dictionary<string, CsvWriter> csvWriters = new();
        Dictionary<string, long> groupCounts = new();
        long totalRecordsInInput = 0;
        while (csvReader.Read())
        {
            totalRecordsInInput++;
            dynamic? record = csvReader.GetRecord<dynamic>();
            string value = csvReader.GetField<string>(columnsToGroupBy);
            MatchCollection matches = regex.Matches(value);
            if (matches.Count == 0)
            {
                const string unmatchedRegexGroup = "NoMatchesOnRegex";
                CsvWriter csvWriter = GetCsvWriter(opts, csvWriters, unmatchedRegexGroup);
                groupCounts.TryGetValue(unmatchedRegexGroup, out long currentCount);
                groupCounts[unmatchedRegexGroup] = currentCount + 1;
                csvWriter.WriteRecord(record);
                csvWriter.NextRecord();
                continue;
            }

            if (opts.RowPerOutputFile && matches.Count > 1)
            {
                const string multipleMatchesOnRegex = "MultipleMatchesOnRegex";
                CsvWriter csvWriter = GetCsvWriter(opts, csvWriters, multipleMatchesOnRegex);
                groupCounts.TryGetValue(multipleMatchesOnRegex, out long currentCount);
                groupCounts[multipleMatchesOnRegex] = currentCount + 1;
                csvWriter.WriteRecord(record);
                csvWriter.NextRecord();
                continue;
            }

            foreach (Match match in matches)
            {
                string @group = match.Groups[1].Value;
                CsvWriter csvWriter = GetCsvWriter(opts, csvWriters, @group);
                groupCounts.TryGetValue(@group, out long currentCount);
                groupCounts[@group] = currentCount + 1;
                csvWriter.WriteRecord(record);
                csvWriter.NextRecord();
            }
        }

        foreach (CsvWriter writer in csvWriters.Values)
        {
            writer.Flush();
        }

        stopwatch.Stop();
        Console.WriteLine($"File processed in {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)} seconds.");
        Console.WriteLine($"Rows in input file: {totalRecordsInInput}");
        foreach ((string? group, long count) in groupCounts.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{group}: {count}");
        }

        Console.WriteLine("Exiting...");
    }

    private static CsvWriter GetCsvWriter(Options opts, IDictionary<string, CsvWriter> csvWriters, string group)
    {
        if (csvWriters.ContainsKey(group))
        {
            return csvWriters[group];
        }

        StreamWriter writer = new(opts.OutputFilePath.Replace(OutputFileNameGroupReplacementString, group), false);
        CsvWriter csvWriter = new(writer, CultureInfo.InvariantCulture);
        csvWriters[group] = csvWriter;
        return csvWriter;
    }
}