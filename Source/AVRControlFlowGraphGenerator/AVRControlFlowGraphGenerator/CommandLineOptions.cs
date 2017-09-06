using CommandLine;

namespace AVRControlFlowGraphGenerator
{
    public class CommandLineOptions
    {
        [Option('i', "input", Required = true, HelpText = "Input file (.HEX) for which to generate a CFG.")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output file (.DOT).")]
        public string OutputFile { get; set; }

        [Option('s', "startaddress", Required = false, Default = 0, HelpText = "Start Offset (defaults to 0x0).")]
        public int StartAddress { get; set; }

        [Option('d', "dpi", Required = false, Default = 200, HelpText = "DPI.")]
        public int DPI { get; set; }
    }
}
