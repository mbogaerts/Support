using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace ExecutorWrapper {
    class Program {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;

        static void Main(string[] args){
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            var arguments = string.Join(" ", args.Skip(1));
            var debugMode = arguments.Contains("-d:");
            var processStartInfo = new ProcessStartInfo { FileName = args[0], Arguments = arguments, UseShellExecute = debugMode, CreateNoWindow = debugMode };
            var process = new Process(){StartInfo = processStartInfo};
            process.Start();
            process.WaitForExit();
        }
    }
}
