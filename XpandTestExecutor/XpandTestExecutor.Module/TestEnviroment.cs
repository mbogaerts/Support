using System.Diagnostics;
using System.IO;
using System.Linq;
using Xpand.Utils.Helpers;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module {
    public static class TestEnviroment {
        static void KillProcessAsUser() {
            var processes = Process.GetProcesses().Where(process => process.ProcessName.Contains("ProcessAsUser")).ToArray();
            foreach (var process in processes) {
                process.Kill();
            }
        }

        public static void KillWebDev(string name) {
            EnviromentEx.KillProccesses(name, i => Process.GetProcessById(i).ProcessName.StartsWith("WebDev.WebServer40"));
        }

        public static void Cleanup(EasyTest[] easyTests) {
        }

        public static void Setup(this EasyTestExecutionInfo info,bool unlink) {
            TestUpdater.UpdateTestConfig(info, unlink);
            AppConfigUpdater.Update(info,unlink);
            TestUpdater.UpdateTestFile(info,unlink);
        }

        public static void Setup(EasyTest[] easyTests) {
            OptionsProvider.Init(easyTests.Select(test => test.FileName).ToArray());
            KillProcessAsUser();
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(easyTests.First().FileName)+"","processasuser.exe"))) {
                var fileName = Path.GetFullPath(@"..\CopyEasyTestReqs.bat");
                var processStartInfo = new ProcessStartInfo(fileName) {
                    WorkingDirectory = Path.GetDirectoryName(fileName) + ""
                };
                var process = new Process { StartInfo = processStartInfo };
                process.Start();
                process.WaitForExit();
            }
        }
    }
}