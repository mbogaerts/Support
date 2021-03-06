﻿using System;
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

        public static readonly Dictionary<Guid, Process> Processes = new Dictionary<Guid, Process>();
        private static void RunTest(Guid easyTestKey, IDataLayer dataLayer, bool rdc, bool debugMode) {
            CustomProcess process;
            int timeout;
            string easyTestName;
            lock (_locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    easyTestName = easyTest.Name+"/"+easyTest.Application;
                    Tracing.Tracer.LogValue("RunTest",easyTestName);
                    timeout = easyTest.Options.DefaultTimeout*60*1000;
                    try {
                        var lastEasyTestExecutionInfo = easyTest.LastEasyTestExecutionInfo;
                        var user = lastEasyTestExecutionInfo.WindowsUser;
                        easyTest.LastEasyTestExecutionInfo.Setup(false);
                        process = new CustomProcess(easyTest,user,rdc,debugMode);

                        process.Start();
                        Processes[easyTestKey] = process;
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

            var complete = Task.Factory.StartNew(() =>{
                Tracing.Tracer.LogValue("WaitForExit", easyTestName);
                process.WaitForExit();
            }).TimeoutAfter(timeout).WaitToCompleteOrTimeOut();
            if (!complete)
                Tracing.Tracer.LogValue("TimeOut", easyTestName);

            Tracing.Tracer.LogValue("CloseRDClient",easyTestName);
            process.CloseRDClient();
            try {
                AfterProcessExecute(dataLayer, easyTestKey);
            }
            catch (Exception e) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    LogErrors(easyTest, e);
                }
            }
            
        }

        private static void LogErrors(EasyTest easyTest, Exception e) {
            lock (_locker) {
                Tracing.Tracer.LogSeparator("LogErrors");
                var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                var logTests = new LogTests();
                foreach (var application in easyTest.Options.Applications.Cast<TestApplication>()) {
                    var logTest = new LogTest { ApplicationName = application.Name, Result = "Failed" };
                    var logError = new LogError { Message = { Text = e.ToString() } };
                    logTest.Errors.Add(logError);
                    logTests.Tests.Add(logTest);
                }
                logTests.Save(Path.Combine(directoryName, "TestsLog.xml"));
                EnviromentEx.LogOffUser(easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                
                easyTest.LastEasyTestExecutionInfo.Update(EasyTestState.Failed);
                easyTest.LastEasyTestExecutionInfo.Setup(true);
                easyTest.Session.ValidateAndCommitChanges();
                Tracing.Tracer.LogError(e);
            }
            
        }
//        private static ProcessStartInfo GetProcessStartInfo(EasyTest easyTest, WindowsUser user, bool rdc, bool debugMode) {
//            string testExecutor = string.Format("TestExecutor.v{0}.exe", AssemblyInfo.VersionShort);
//            var args = debugMode?@" -a '-d:'":null;
//            string shell=debugMode?"-s":null;
//            string arguments = rdc ? string.Format("-e " + testExecutor + " -u {0} -p {1} -t {3} -d {4} -n {5} "+shell+args
//                , user.Name, user.Password,args, easyTest.Options.DefaultTimeout*60*1000, WindowsUser.Domain, Path.GetFileName(easyTest.FileName)): args;
//            string directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
//            var exe = rdc ? "ProcessAsUser.exe" : testExecutor;
////            arguments = @"/accepteula -h -w """+directoryName + @""" -i -s """ + Path.Combine(directoryName, exe) + @""" " + arguments;
////            exe = "psexec.exe";
//            var processStartInfo = new ProcessStartInfo(exe, arguments) {
//                UseShellExecute = debugMode,
//                WorkingDirectory = directoryName
//            };
//            if (!debugMode) {
//                processStartInfo.RedirectStandardOutput = true;
//                processStartInfo.RedirectStandardError = true;
//            }
//            return processStartInfo;
//        }

        private static void AfterProcessExecute(IDataLayer dataLayer, Guid easyTestKey) {
            lock (_locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    Tracing.Tracer.LogValue("In AfterProcessExecute", easyTest.Name + "/" + easyTest.Application);
                    var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                    CopyXafLogs(directoryName);
                    var logTests = easyTest.GetLogTests();
                    var state = EasyTestState.Passed;
                    if (logTests.Any(test => test.Result != "Passed")) {
                        state = EasyTestState.Failed;
                    }
                    Tracing.Tracer.LogValue("State", state.ToString());
                    easyTest.LastEasyTestExecutionInfo.Update(state);
                    if (easyTest.LastEasyTestExecutionInfo.ExecutedFromOtherUser()) {
                        EnviromentEx.LogOffUser(easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                        Tracing.Tracer.LogValue("LogOffUser", easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                        easyTest.LastEasyTestExecutionInfo.Setup(true);
                        easyTest.IgnoreApplications(logTests);
                        Tracing.Tracer.LogValue("Execution", "Reset");
                    }
                    easyTest.Session.ValidateAndCommitChanges();
                    Tracing.Tracer.LogValue("Out AfterProcessExecute", easyTest.Name+"/"+easyTest.Application);
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

        public static void Execute(string fileName, bool rdc) {
            var easyTests = GetEasyTests(fileName);
            Execute(easyTests, rdc,false, task => { });
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

        public static CancellationTokenSource Execute(EasyTest[] easyTests, bool rdc, bool debugMode, Action<Task> continueWith ) {
            Tracing.Tracer.LogValue("EasyTests.Count", easyTests.Count());
            if (easyTests.Any()) {
                InitProcessDictionary(easyTests);
                TestEnviroment.Setup(easyTests);
                var tokenSource = new CancellationTokenSource();
                Task.Factory.StartNew(() => ExecuteCore(easyTests, rdc,  tokenSource.Token,debugMode),tokenSource.Token).ContinueWith(task =>{
                    TestEnviroment.Cleanup(easyTests);
                    Tracing.Tracer.LogText("Main thread finished");
                    continueWith(task);
                }, tokenSource.Token);
                Thread.Sleep(100);
                return tokenSource;
            }
            return null;
        }

        private static void InitProcessDictionary(IEnumerable<EasyTest> easyTests){
            lock (_locker){
                Processes.Clear();
                foreach (var easyTest in easyTests) {
                    Processes.Add(easyTest.Oid, null);
                }
            }
        }

        private static void ExecuteCore(EasyTest[] easyTests, bool rdc, CancellationToken token, bool debugMode) {
            string fileName = null;
            try {
                var dataLayer = GetDatalayer();
                var executionInfoKey = CreateExecutionInfoKey(dataLayer, rdc, easyTests);
                do {
                    if (token.IsCancellationRequested)
                        return;
                    var easyTest = GetNextEasyTest(executionInfoKey, easyTests, dataLayer, rdc);
                    if (easyTest != null) {
                        fileName = easyTest.FileName;
                        Task.Factory.StartNew(() => RunTest(easyTest.Oid, dataLayer, rdc,debugMode), token).TimeoutAfter(easyTest.Options.DefaultTimeout*60*1000);
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

        private static Guid CreateExecutionInfoKey(IDataLayer dataLayer, bool rdc, EasyTest[] easyTests) {
            Guid executionInfoKey;
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = ExecutionInfo.Create(unitOfWork, rdc);
                if (rdc)
                    EnviromentEx.LogOffAllUsers(executionInfo.WindowsUsers.Select(user => user.Name).ToArray());
                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                foreach (var easyTest in easyTests) {
                    easyTest.CreateExecutionInfo(rdc, executionInfo);
                }
                unitOfWork.ValidateAndCommitChanges();
                CurrentSequenceOperator.CurrentSequence = executionInfo.Sequence;
                executionInfoKey = executionInfo.Oid;
            }
            return executionInfoKey;
        }

        private static EasyTest GetNextEasyTest(Guid executionInfoKey, EasyTest[] easyTests, IDataLayer dataLayer, bool rdc) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey);
//                KillTimeoutProccesses(executionInfo);

                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                var runningInfosCount = executionInfo.EasyTestRunningInfos.Count();
                if (runningInfosCount < executionInfo.WindowsUsers.Count()) {
                    var easyTest = GetNeverRunEasyTest(easyTests, executionInfo) ?? GetFailedEasyTest(easyTests, executionInfo, rdc);
                    if (easyTest != null) {
                        easyTest.LastEasyTestExecutionInfo.State = EasyTestState.Running;
                        easyTest.Session.ValidateAndCommitChanges();
                        return easyTest;
                    }
                }
            }
            return null;
        }

        private static EasyTest GetFailedEasyTest(EasyTest[] easyTests, ExecutionInfo executionInfo, bool rdc) {
            for (int i = 0; i < ((IModelOptionsTestExecutor)CaptionHelper.ApplicationModel.Options).ExecutionRetries; i++) {
                var easyTest = GetEasyTest(easyTests, executionInfo, i + 1);
                if (easyTest != null) {
                    var windowsUser = executionInfo.GetNextUser(easyTest);
                    if (windowsUser != null) {
                        easyTest.CreateExecutionInfo(rdc, executionInfo, windowsUser);
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

//        private static void KillTimeoutProccesses(ExecutionInfo executionInfo) {
//            return;
//            var timeOutInfos = executionInfo.EasyTestExecutionInfos.Where(IsTimeOut);
//            foreach (var timeOutInfo in timeOutInfos) {
//                LogErrors(timeOutInfo.EasyTest, new TimeoutException(timeOutInfo.EasyTest + " has timeout " + timeOutInfo.EasyTest.Options.DefaultTimeout + " and runs already  for " + DateTime.Now.Subtract(timeOutInfo.Start).TotalMinutes));
//                var process = _processes[timeOutInfo.EasyTest.Oid];
//                if (process != null && !process.HasExited)
//                    process.Kill();
//                Thread.Sleep(2000);
//            }
//        }

//        private static bool IsTimeOut(EasyTestExecutionInfo info) {
//            return info.State==EasyTestState.Running&&DateTime.Now.Subtract(info.Start).TotalMinutes > info.EasyTest.Options.DefaultTimeout;
//        }


        private static EasyTest GetEasyTest(IEnumerable<EasyTest> easyTests, ExecutionInfo executionInfo, int i) {
            return executionInfo.GetTestsToExecute(i).FirstOrDefault(easyTests.Contains);
        }


        public static void Execute(string fileName, bool rdc, Action<Task> continueWith, bool debugMode) {
            var easyTests = GetEasyTests(fileName);
            Execute(easyTests, rdc, debugMode,continueWith);
        }
    }
}