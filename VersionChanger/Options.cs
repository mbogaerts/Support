using CommandLine;
using CommandLine.Text;

namespace VersionChanger {
    public class Options {
        [Option('v', "Version", Required = true, HelpText = "The version")]
        public string Version { get; set; }
        [Option('a', "AssemblyInfoPath",  DefaultValue = @"..\Xpand\Xpand.Utils\Properties")]
        public string AssemblyInfoPath { get; set; }
        [Option('t', "VSTemplates",  DefaultValue = @"..\Support\Xpand.DesignExperience")]
        public string VSTemplates { get; set; }
        [Option('b', "batch", DefaultValue = @"..\buildAll.cmd")]
        public string BuildBatch { get; set; }


        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this,current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
