using System.Diagnostics;
using System.Globalization;
using CommandLine;
using CsvHelper;

namespace CsvColumnSelector;

public class Program
{
    internal class Options
    {
        [Option('i', "input", Required = true, HelpText = "Path for input file.")]
        public string InputFilePath { get; set; } = null!;

        [Option('c', "column", Required = true,
            HelpText =
                "Columns to select from input file. Format 'ColumnName' or 'InputColumnName:OutputColumnName'.")]

        public IEnumerable<string> Columns { get; set; } = null!;

        [Option('o', "output", Required = true, HelpText = "Path for output file.")]
        public string OutputFilePath { get; set; } = null!;
    }

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(SelectCsvColumns)
            .WithNotParsed(_ => { Console.WriteLine("Failed to parse arguments."); });
    }

    private static void SelectCsvColumns(Options opts)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        List<string> columnsToSelect = new();
        List<string> columnNamesToUse = new();
        foreach (string column in opts.Columns)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                Console.WriteLine($"Invalid column argument: '{column}'.");
                Console.WriteLine("Exiting...");
                return;
            }

            string[] columnsNames = column.Split(":");
            switch (columnsNames.Length)
            {
                case 1:
                    columnsToSelect.Add(column);
                    columnNamesToUse.Add(column);
                    break;
                case 2:
                    columnsToSelect.Add(columnsNames[0]);
                    columnNamesToUse.Add(columnsNames[1]);
                    break;
                default:
                    Console.WriteLine($"Invalid format for column argument: '{column}'. Only one ':' is allowed.");
                    Console.WriteLine("Exiting...");
                    break;
            }
        }


        using StreamReader reader = new(opts.InputFilePath);
        using CsvReader csvReader = new(reader, CultureInfo.InvariantCulture);
        csvReader.Read();
        csvReader.ReadHeader();

        using StreamWriter writer = new(opts.OutputFilePath, false);
        using CsvWriter csvWriter = new(writer, CultureInfo.InvariantCulture);

        // Write header.
        foreach (string columnName in columnNamesToUse)
        {
            csvWriter.WriteField(columnName);
        }

        csvWriter.NextRecord();

        // Write data.
        while (csvReader.Read())
        {
            foreach (string columnName in columnsToSelect)
            {
                string value = csvReader.GetField<string>(columnName);
                csvWriter.WriteField(value);
            }

            csvWriter.NextRecord();
        }

        stopwatch.Stop();
        Console.WriteLine($"File processed in {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)} seconds.");
        Console.WriteLine("Exiting...");
    }
}