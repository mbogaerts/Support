using CommandLine;
using CommandLine.Text;

namespace ProcessAsUserWrapper {
    public class Options {
        [Option('e', "path", Required = true, HelpText = "The process path")]
        public string ExePath { get; set; }

        [Option('a', "arguments", Required = true, HelpText = "The process arguments")]
        public string Arguments { get; set; }

        [Option('s', "shell", HelpText = "Display command prompt")]
        public bool Shell { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}