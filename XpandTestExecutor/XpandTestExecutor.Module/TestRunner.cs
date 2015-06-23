using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp.Utils;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Utils.Helpers;
using Xpand.Utils.Threading;
using XpandTestExecutor.Module.BusinessObjects;
using XpandTestExecutor.Module.Controllers;

namespace XpandTestExecutor.Module {
    public class TestRunner {
        public const string EasyTestUsersDir = "EasyTestUsers";
        private static readonly object _locker = new object();

        private static bool ExecutionFinished(IDataLayer dataLayer, Guid executionInfoKey, int testsCount) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey, true);
                var ret = executionInfo.FinishedEasyTests() == testsCount;
                if (ret)
                    Tracing.Tracer.LogText("ExecutionFinished for Seq "+executionInfo.Sequence);
                return ret;
            }
        }

        static readonly Dictionary<Guid, Process> _processes = new Dictionary<Guid, Process>();
        private static void RunTest(Guid easyTestKey, IDataLayer dataLayer, bool isSystem,bool debugMode) {
            Process process;
            int timeout;
            lock (_locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    timeout = easyTest.Options.DefaultTimeout*60*1000;
                    try {
                        var lastEasyTestExecutionInfo = easyTest.LastEasyTestExecutionInfo;
                        var user = lastEasyTestExecutionInfo.WindowsUser;
                        easyTest.LastEasyTestExecutionInfo.Setup(false);
                        var processStartInfo = GetProcessStartInfo(easyTest, user, isSystem,debugMode);

                        process = new Process {
                            StartInfo = processStartInfo
                        };
                        process.Start();
                        _processes[easyTestKey] = process;
                        lastEasyTestExecutionInfo =
                            unitOfWork.GetObjectByKey<EasyTestExecutionInfo>(lastEasyTestExecutionInfo.Oid, true);
                        lastEasyTestExecutionInfo.Update(EasyTestState.Running);
                        unitOfWork.ValidateAndCommitChanges();

                        Thread.Sleep(5000);
                    }
                    catch (Exception e) {
                        LogErrors(easyTest, e);
                        throw;
                    }
                }
            }
            
            var task = Task.Factory.StartNew(() => process.WaitForExit(timeout)).TimeoutAfter(timeout);
            Task.WaitAll(task);
            AfterProcessExecute(dataLayer, easyTestKey);
            
        }

        private static void LogErrors(EasyTest easyTest, Exception e) {
            lock (_locker) {
                var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                var logTests = new LogTests();
                foreach (var application in easyTest.Options.Applications.Cast<TestApplication>()) {
                    var logTest = new LogTest { ApplicationName = application.Name, Result = "Failed" };
                    var logError = new LogError { Message = { Text = e.ToString() } };
                    logTest.Errors.Add(logError);
                    logTests.Tests.Add(logTest);
                }
                logTests.Save(Path.Combine(directoryName, "TestsLog.xml"));
                easyTest.LastEasyTestExecutionInfo.Update(EasyTestState.Failed);
                easyTest.Session.ValidateAndCommitChanges();
            }
            Tracing.Tracer.LogError(e);
        }
        private static ProcessStartInfo GetProcessStartInfo(EasyTest easyTest, WindowsUser user, bool isSystem, bool debugMode) {
            string testExecutor = string.Format("TestExecutor.v{0}.exe", AssemblyInfo.VersionShort);
            string arguments = isSystem
                ? string.Format("-e " + testExecutor + " -u {0} -p {1} -a {2} -t {3}", user.Name, user.Password,
                    GetArguments(easyTest, debugMode), easyTest.Options.DefaultTimeout*60*1000)
                : Path.GetFileName(easyTest.FileName);
            string directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
            var exe = isSystem ? "ProcessAsUser.exe" : testExecutor;
            var processStartInfo = new ProcessStartInfo(Path.Combine(directoryName, exe), arguments) {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directoryName
            };
            return processStartInfo;
        }

        private static string GetArguments(EasyTest easyTest, bool debugMode) {
            var fileName = Path.GetFileName(easyTest.FileName);
            return debugMode ? fileName + " -d:" : fileName;
        }

        private static void AfterProcessExecute(IDataLayer dataLayer, Guid easyTestKey) {
            lock (_locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                    CopyXafLogs(directoryName);
                    var logTests = easyTest.GetLogTests();
                    var state = EasyTestState.Passed;
                    if (logTests.All(test => test.Result == "Passed")) {
                        Tracing.Tracer.LogText(easyTest.FileName + " passed");
                    }
                    else {
                        Tracing.Tracer.LogText(easyTest.FileName + " not passed=" + string.Join(Environment.NewLine,
                            logTests.SelectMany(test => test.Errors.Select(error => error.Message.Text))));
                        state = EasyTestState.Failed;
                    }

                    

                    easyTest.LastEasyTestExecutionInfo.Update(state);
                    easyTest.Session.ValidateAndCommitChanges();
                    if (easyTest.LastEasyTestExecutionInfo.ExecutedFromSystem()) {
                        EnviromentEx.LogOffUser(easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                        easyTest.LastEasyTestExecutionInfo.Setup(true);
                        easyTest.IgnoreApplications(logTests);
                    }
                }
            }
        }

        private static void CopyXafLogs(string directoryName) {
            string fileName = Path.Combine(directoryName, "config.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, directoryName);
                foreach (var alias in options.Aliases.Cast<TestAlias>().Where(@alias => alias.ContainsAppPath())) {
                    var suffix = alias.IsWinAppPath() ? "_win" : "_web";
                    var sourceFileName = Path.Combine(alias.Value, "eXpressAppFramework.log");
                    if (File.Exists(sourceFileName)) {
                        File.Copy(sourceFileName, Path.Combine(directoryName, "eXpressAppFramework" + suffix + ".log"), true);
                    }
                }
            }
        }

        public static void Execute(string fileName, bool isSystem) {
            var easyTests = GetEasyTests(fileName);
            Execute(easyTests, isSystem,false, task => { });
        }

        public static EasyTest[] GetEasyTests(string fileName) {
            var fileNames = File.ReadAllLines(fileName).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            ApplicationHelper.Instance.Application.ObjectSpaceProvider.UpdateSchema();
            var objectSpace = ApplicationHelper.Instance.Application.ObjectSpaceProvider.CreateObjectSpace();
            OptionsProvider.Init(fileNames);
            var easyTests = EasyTest.GetTests(objectSpace, fileNames);
            objectSpace.Session().ValidateAndCommitChanges();
            return easyTests;
        }

        public static CancellationTokenSource Execute(EasyTest[] easyTests, bool isSystem,bool debugMode, Action<Task> continueWith) {
            Tracing.Tracer.LogValue("EasyTests.Count", easyTests.Count());
            if (easyTests.Any()) {
                InitProcessDictionary(easyTests);
                TestEnviroment.Setup(easyTests);
                var tokenSource = new CancellationTokenSource();
                Task.Factory.StartNew(() => ExecuteCore(easyTests, isSystem,  tokenSource.Token,debugMode),tokenSource.Token).ContinueWith(task =>{
                    TestEnviroment.Cleanup(easyTests);
                    Trace.TraceInformation("Main thread finished");
                    continueWith(task);
                }, tokenSource.Token);
                Thread.Sleep(100);
                return tokenSource;
            }
            return null;
        }

        private static void InitProcessDictionary(IEnumerable<EasyTest> easyTests) {
            _processes.Clear();
            foreach (var easyTest in easyTests) {
                _processes.Add(easyTest.Oid, null);
            }
        }

        private static void ExecuteCore(EasyTest[] easyTests, bool isSystem, CancellationToken token,bool debugMode) {
            string fileName = null;
            try {
                var dataLayer = GetDatalayer();
                var executionInfoKey = CreateExecutionInfoKey(dataLayer, isSystem, easyTests);
                do {
                    if (token.IsCancellationRequested)
                        return;
                    var easyTest = GetNextEasyTest(executionInfoKey, easyTests, dataLayer, isSystem);
                    if (easyTest != null) {
                        fileName = easyTest.FileName;
                        Task.Factory.StartNew(() => RunTest(easyTest.Oid, dataLayer, isSystem,debugMode), token).TimeoutAfter(easyTest.Options.DefaultTimeout*60*1000);
                    }
                    Thread.Sleep(10000);
                } while (!ExecutionFinished(dataLayer, executionInfoKey, easyTests.Length));
            }
            catch (Exception e) {
                Tracing.Tracer.LogError(new Exception("ExecutionCore Exception on " + fileName,e));
                throw;
            }
        }

        private static IDataLayer GetDatalayer() {
            var xpObjectSpaceProvider = new XPObjectSpaceProvider(new ConnectionStringDataStoreProvider(ApplicationHelper.Instance.Application.ConnectionString), true);
            return xpObjectSpaceProvider.CreateObjectSpace().Session().DataLayer;
        }

        private static Guid CreateExecutionInfoKey(IDataLayer dataLayer, bool isSystem, EasyTest[] easyTests) {
            Guid executionInfoKey;
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = ExecutionInfo.Create(unitOfWork, isSystem);
                if (isSystem)
                    EnviromentEx.LogOffAllUsers(executionInfo.WindowsUsers.Select(user => user.Name).ToArray());
                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                foreach (var easyTest in easyTests) {
                    easyTest.CreateExecutionInfo(isSystem, executionInfo);
                }
                unitOfWork.ValidateAndCommitChanges();
                CurrentSequenceOperator.CurrentSequence = executionInfo.Sequence;
                executionInfoKey = executionInfo.Oid;
            }
            return executionInfoKey;
        }

        private static EasyTest GetNextEasyTest(Guid executionInfoKey, EasyTest[] easyTests, IDataLayer dataLayer, bool isSystem) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey);
                KillTimeoutProccesses(executionInfo);

                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                var runningInfosCount = executionInfo.EasyTestRunningInfos.Count();
                if (runningInfosCount < executionInfo.WindowsUsers.Count()) {
                    var easyTest = GetNeverRunEasyTest(easyTests, executionInfo) ?? GetFailedEasyTest(easyTests, executionInfo, isSystem);
                    if (easyTest != null) {
                        easyTest.LastEasyTestExecutionInfo.State = EasyTestState.Running;
                        easyTest.Session.ValidateAndCommitChanges();
                        return easyTest;
                    }
                }
            }
            return null;
        }

        private static EasyTest GetFailedEasyTest(EasyTest[] easyTests, ExecutionInfo executionInfo, bool isSystem) {
            for (int i = 0; i < ((IModelOptionsTestExecutor)CaptionHelper.ApplicationModel.Options).ExecutionRetries; i++) {
                var easyTest = GetEasyTest(easyTests, executionInfo, i + 1);
                if (easyTest != null) {
                    var windowsUser = executionInfo.GetNextUser(easyTest);
                    if (windowsUser != null) {
                        easyTest.CreateExecutionInfo(isSystem, executionInfo, windowsUser);
                        return easyTest;
                    }
                    return null;
                }
            }
            return null;
        }

        private static EasyTest GetNeverRunEasyTest(IEnumerable<EasyTest> easyTests, ExecutionInfo executionInfo) {
            var neverRunTest = GetEasyTest(easyTests, executionInfo, 0);
            if (neverRunTest != null) {
                var windowsUser = executionInfo.GetNextUser(neverRunTest);
                if (windowsUser != null) {
                    neverRunTest.LastEasyTestExecutionInfo.WindowsUser = windowsUser;
                    return neverRunTest;
                }
            }
            return null;
        }

        private static void KillTimeoutProccesses(ExecutionInfo executionInfo) {
            return;
            var timeOutInfos = executionInfo.EasyTestExecutionInfos.Where(IsTimeOut);
            foreach (var timeOutInfo in timeOutInfos) {
                LogErrors(timeOutInfo.EasyTest, new TimeoutException(timeOutInfo.EasyTest + " has timeout " + timeOutInfo.EasyTest.Options.DefaultTimeout + " and runs already  for " + DateTime.Now.Subtract(timeOutInfo.Start).TotalMinutes));
                var process = _processes[timeOutInfo.EasyTest.Oid];
                if (process != null && !process.HasExited)
                    process.Kill();
                Thread.Sleep(2000);
            }
        }

        private static bool IsTimeOut(EasyTestExecutionInfo info) {
            return info.State==EasyTestState.Running&&DateTime.Now.Subtract(info.Start).TotalMinutes > info.EasyTest.Options.DefaultTimeout;
        }


        private static EasyTest GetEasyTest(IEnumerable<EasyTest> easyTests, ExecutionInfo executionInfo, int i) {
            return executionInfo.GetTestsToExecute(i).FirstOrDefault(easyTests.Contains);
        }


        public static void Execute(string fileName, bool isSystem, Action<Task> continueWith,bool debugMode) {
            var easyTests = GetEasyTests(fileName);
            Execute(easyTests, isSystem, debugMode,continueWith);
        }
    }
}