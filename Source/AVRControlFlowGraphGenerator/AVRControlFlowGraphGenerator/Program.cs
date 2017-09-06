using System;
using CommandLine;
using NLog;

namespace AVRControlFlowGraphGenerator
{
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetLogger(AVRControlFlowGraphGenerator.LoggerName);

        private static void Main(string[] args)
        {
            Logger.Info("Parsing Command Line Arguments...");
            Parser.Default.ParseArguments<CommandLineOptions>(args)
                .WithParsed(options =>
                {
                    var generationOptions = new AVRControlFlowGraphGeneratorOptions
                    {
                        InputFile = options.InputFile,
                        OutputFile = options.OutputFile,
                        StartAddress = options.StartAddress,
                        DPI = options.DPI
                    };

                    try
                    {
                        var generator = new AVRControlFlowGraphGenerator(generationOptions);
                        generator.GenerateCFG();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Unexpected runtime error: {ex.Message}!");
                        Environment.Exit((int) ExitCode.RuntimeError);
                    }
                    Environment.Exit((int) ExitCode.Success);
                })
                .WithNotParsed(options =>
                    Environment.Exit((int) ExitCode.FailedToParseCommandLineArgs));
        }
    }
}