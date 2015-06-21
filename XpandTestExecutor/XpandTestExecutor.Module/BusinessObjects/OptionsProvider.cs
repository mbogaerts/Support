using System.Collections.Generic;
using System.IO;
using DevExpress.EasyTest.Framework;

namespace XpandTestExecutor.Module.BusinessObjects {
    public class OptionsProvider {
        private static readonly OptionsProvider _optionsProvider=new OptionsProvider();
        Dictionary<string,Options> _options;

        public static OptionsProvider Instance {
            get { return _optionsProvider; }
        }

        public Options this[string fileName] {
            get {

                return _options[fileName.ToLower()];
            }
        }

        public static void Init(string[] easyTestFileNames) {
            Instance._options = new Dictionary<string, Options>();
            foreach (var path in easyTestFileNames) {
                var directoryName = Path.GetDirectoryName(path) + "";
                string fileName = Path.Combine(directoryName, "config.xml");
                var destFileName = Path.Combine(Path.GetDirectoryName(fileName)+"","_"+Path.GetFileName(fileName));
                if (!File.Exists(destFileName))
                    File.Copy(fileName, destFileName,true);
                Options options;
                using (var fileStream = new FileStream(destFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) {
                    options = Options.LoadOptions(fileStream, null, null, directoryName);
                }
                Instance._options.Add(path.ToLower(), options);
            }
        }
    }
}
