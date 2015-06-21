using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using DevExpress.EasyTest.Framework;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module {
    public static class TestUpdater {

        public static void IgnoreApplications(this EasyTest easyTest, LogTest[] logTests) {
            var options = easyTest.Options;
            foreach (var logTest in logTests.Where(test => test.Result == "Passed")) {
                var testApplication = options.Applications.Cast<TestApplication>().First(application => application.Name == logTest.ApplicationName);
                testApplication.Ignored=true;
            }
            easyTest.SerializeOptions();
        }

        public static void UpdateTestConfig(EasyTestExecutionInfo easyTestExecutionInfo, bool unlink) {
            if (unlink) {
                string fileName = Path.Combine(Path.GetDirectoryName(easyTestExecutionInfo.EasyTest.FileName)+"", "_config.xml");
                File.Copy(fileName, Path.Combine(Path.GetDirectoryName(easyTestExecutionInfo.EasyTest.FileName) + "", "config.xml"),true);
            }
            else {
                UpdatePort(easyTestExecutionInfo);
                UpdateApplications(easyTestExecutionInfo);
                UpdateAppBinAlias(easyTestExecutionInfo);
                UpdateDataBases(easyTestExecutionInfo);
                easyTestExecutionInfo.EasyTest.SerializeOptions();
            }

        }

        private static void UpdateApplications(EasyTestExecutionInfo easyTestExecutionInfo) {
            var testApplications =easyTestExecutionInfo.EasyTest.Options.Applications.Cast<TestApplication>();
            foreach (var testApplication in testApplications) {
                var xmlAttribute = testApplication.AdditionalAttributes.FirstOrDefault(attribute => attribute.Name=="FileName");                
                if (xmlAttribute != null) {
                    var currentPath = xmlAttribute.Value;
                    if (!currentPath.Contains(TestRunner.EasyTestUsersDir)){
                        var newPath = Path.Combine(Path.GetDirectoryName(currentPath) + @"\",
                            TestRunner.EasyTestUsersDir + @"\" + easyTestExecutionInfo.WindowsUser.Name);
                        xmlAttribute.Value = Path.Combine(newPath, Path.GetFileName(currentPath) + "");
                    }
                }
                else {
                    xmlAttribute = testApplication.AdditionalAttributes.First(attribute => attribute.Name == "PhysicalPath");                
                    if (!xmlAttribute.Value.Contains(TestRunner.EasyTestUsersDir))
                        xmlAttribute.Value=Path.Combine(xmlAttribute.Value,TestRunner.EasyTestUsersDir+@"\"+easyTestExecutionInfo.WindowsUser.Name);
                }
            }
        }

        private static void UpdateDataBases(EasyTestExecutionInfo easyTestExecutionInfo,bool unlink=false) {
            foreach (var testDatabase in easyTestExecutionInfo.EasyTest.Options.TestDatabases.Cast<TestDatabase>()) {
                var suffix = !unlink ? "_" + easyTestExecutionInfo.WindowsUser.Name : null;
                testDatabase.DBName = testDatabase.DefaultDBName() + suffix;
            }
        }

        private static void UpdatePort(EasyTestExecutionInfo easyTestExecutionInfo) {
            foreach (var application in easyTestExecutionInfo.EasyTest.Options.Applications.Cast<TestApplication>()) {
                var additionalAttribute =
                    application.AdditionalAttributes.FirstOrDefault(
                        attribute => attribute.LocalName.ToLowerInvariant() == "communicationport");
                if (additionalAttribute != null)
                    additionalAttribute.Value = easyTestExecutionInfo.WinPort+"";
                else {
                    additionalAttribute =
                        application.AdditionalAttributes.First(attribute => attribute.LocalName.ToLowerInvariant() == "url");
                    additionalAttribute.Value = "http://localhost:" + easyTestExecutionInfo.WebPort;
                }
            }
        }

        private static void UpdateTestFileCore(string fileName, WindowsUser windowsUser, Options options,bool unlink) {
            var allText = File.ReadAllText(fileName);
            foreach (var testDatabase in options.TestDatabases.Cast<TestDatabase>()) {
                var suffix = !unlink ? "_" + windowsUser.Name : null;
                allText = Regex.Replace(allText, @"(" + testDatabase.DefaultDBName() + @")(_[^\s]*)?", "$1" + suffix, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            File.WriteAllText(fileName, allText);
        }

        public static void UpdateTestFile(EasyTestExecutionInfo easyTestExecutionInfo,bool unlink) {
            var xmlSerializer = new XmlSerializer(typeof(Options));
            Options options;

            var path = Path.Combine(Path.GetDirectoryName(easyTestExecutionInfo.EasyTest.FileName) + "", "config.xml");
            using (var streamReader = new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)) {
                options = (Options)xmlSerializer.Deserialize(streamReader);
            }
            var windowsUser = easyTestExecutionInfo.WindowsUser;
            UpdateTestFileCore(easyTestExecutionInfo.EasyTest.FileName, windowsUser, options,unlink);
            foreach (var includedFile in IncludedFiles(easyTestExecutionInfo.EasyTest.FileName)) {
                UpdateTestFileCore(includedFile, windowsUser, options,unlink);
            }
        }

        private static IEnumerable<string> IncludedFiles(string fileName) {
            var allText = File.ReadAllText(fileName);
            var regexObj = new Regex("#IncludeFile (.*)inc", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Match matchResult = regexObj.Match(allText);
            while (matchResult.Success) {
                yield return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fileName) + "", matchResult.Groups[1].Value + "inc"));
                matchResult = matchResult.NextMatch();
            }
        }


        private static void UpdateAppBinAlias(EasyTestExecutionInfo easyTestExecutionInfo) {
            foreach (var alias in easyTestExecutionInfo.EasyTest.Options.Aliases.Cast<TestAlias>().Where(@alias => alias.ContainsAppPath())) {
                alias.Value = alias.UpdateAppPath(easyTestExecutionInfo.WindowsUser.Name);
            }
        }
    }
}