using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using CommandLine;

namespace VersionChanger {
    class Program {
        static void Main(string[] args) {
            var options=new Options();
            bool arguments = Parser.Default.ParseArguments(args, options);
            if (arguments){
                try{
                    var newVersion = new Version(options.Version);
                    var fullPath = Path.GetFullPath(options.AssemblyInfoPath);
                    var assemblyInfo = Path.Combine(fullPath,"assemblyinfo.cs");
                    if (!File.Exists(assemblyInfo))
                        throw new FileNotFoundException(assemblyInfo);
                    var regexObj = new Regex("public const string Version = \"([^\"]*)");
                    var allText = File.ReadAllText(assemblyInfo);
                    var currentVersion = new Version(regexObj.Match(allText).Groups[1].Value);
                    if (currentVersion != newVersion){
                        allText = Regex.Replace(allText, "(?<start>.*public const string Version = \")[^\"]*(?<end>.*)", "${start}" +newVersion+ "${end}", RegexOptions.Multiline);
                        File.WriteAllText(assemblyInfo, allText);
                        var vsTemplates = Path.GetFullPath(options.VSTemplates + @"\vs_templates");
                        UnZip(vsTemplates, currentVersion);
                        var buildBatch = Path.GetFullPath(options.BuildBatch);
                        var processStartInfo = new ProcessStartInfo(buildBatch){
                            WorkingDirectory = Path.GetDirectoryName(buildBatch)+""
                        };
                        var process = new Process{ StartInfo = processStartInfo };
                        process.Start();
                        process.WaitForExit();
                        Zip(vsTemplates, currentVersion);
                    }

                }
                catch (Exception e){
                    Console.WriteLine(e);
                }
            }
            else{
                Console.WriteLine(options.GetUsage());
            }
            if (Debugger.IsAttached){
                Console.ReadLine();
            }
        }

        private static void Zip(string vsTemplates, Version currentVersion){
            ZipCore(vsTemplates, currentVersion, "cs");
            ZipCore(vsTemplates, currentVersion, "vb");
        }

        private static void ZipCore(string vsTemplates, Version currentVersion, string lang){
            var zipFile = GetZipFile(vsTemplates, currentVersion, lang);
            var tempFilename = Path.Combine(Path.GetTempPath(),Path.GetFileName(zipFile)+"");
            ZipFile.CreateFromDirectory(vsTemplates + @"\" + lang, tempFilename);
            File.Copy(tempFilename,zipFile);
            File.Delete(tempFilename);
            DeleteAllExpeptTemplate(vsTemplates + @"\" + lang);
        }

        private static void UnZip(string vsTemplates, Version currentVersion){
            UnzipCore(vsTemplates, currentVersion,"Cs");
            UnzipCore(vsTemplates, currentVersion,"vb");
        }
        
        private static void UnzipCore(string vsTemplates, Version currentVersion, string lang){
            var zipFile = GetZipFile(vsTemplates, currentVersion, lang);
            DeleteAllExpeptTemplate(vsTemplates + @"\" + lang);
            ZipFile.ExtractToDirectory(zipFile, vsTemplates + @"\" + lang);
            File.Delete(zipFile);
        }

        private static void DeleteAllExpeptTemplate(string vsTemplates){
            foreach (var file in Directory.GetFiles(vsTemplates + @"\")){
                if (Path.GetExtension(file) != ".zip")
                    File.Delete(file);
            }
            foreach (var directory in Directory.GetDirectories(vsTemplates+@"\")){
                Directory.Delete(directory,true);
            }
        }

        private static string GetZipFile(string vsTemplates, Version currentVersion, string lang){
            return Path.Combine(vsTemplates + @"\" + lang,
                "XpandFullSolution" + lang + ".v" + currentVersion.Major + "." + currentVersion.Minor + ".zip");
        }
    }
}
