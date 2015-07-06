using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandLine;

namespace ProcessAsUserWrapper {
    static class Program {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args){
            var options = new Options();
            bool arguments = Parser.Default.ParseArguments(args, options);
            var streamWriter = File.CreateText("processAsuserWrapper.log");
            if (arguments) {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var mainForm = new Form1();
                streamWriter.WriteLine("args="+options.Arguments);
                streamWriter.WriteLine("Shell=" + options.Shell);
                mainForm.Load += (sender, eventArgs) =>{
                    var processStartInfo = new ProcessStartInfo(options.ExePath, options.Arguments){
                        UseShellExecute = options.Shell,
                        RedirectStandardOutput = !options.Shell,
                        CreateNoWindow = !options.Shell
                    };
                    var process = Process.Start(processStartInfo);
                    var handle = GetConsoleWindow();
                    ShowWindow(handle, SW_HIDE);
                    Debug.Assert(process != null, "process != null");
                    process.WaitForExit();
                    if (!options.Shell)
                        streamWriter.Write(process.StandardOutput.ReadToEnd());
                    streamWriter.Close();
                    Application.ExitThread();
                };
                Application.Run(mainForm);
            }
            else {
                streamWriter.Write(options.GetUsage());
                streamWriter.Close();
            }
        }

    }
}
