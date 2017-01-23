using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Utils.Helpers;
using XpandTestExecutor.Module.BusinessObjects;
using XpandTestExecutor.Module.Services;

namespace XpandTestExecutor.Module.Controllers {
    public interface IModelOptionsTestExecutor {
        [DefaultValue(5)]
        int ExecutionRetries { get; set; }
    }
    public class TestController : ObjectViewController<ListView, EasyTest>,IModelExtender {
        private const string CancelRun = "Cancel Run";
        private const string Run = "Run";
        private readonly SimpleAction _runTestAction;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly SimpleAction _unlinkTestAction;
        private TestControllerHelper _testControllerHelper;

        public TestController() {
            _runTestAction = new SimpleAction(this, "RunTest", PredefinedCategory.View) {
                Caption = Run,
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects
            };
            _runTestAction.Execute += RunTestActionOnExecute;

            _unlinkTestAction = new SimpleAction(this, "UnlinkTest", PredefinedCategory.View) {
                Caption = "Unlink",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects
            };
            _unlinkTestAction.Execute+=UnlinkTestActionOnExecute;   
        }

        protected override void OnActivated() {
            base.OnActivated();
            _testControllerHelper = Application.MainWindow.GetController<TestControllerHelper>();
        }

        public SingleChoiceAction SelectionModeAction => _testControllerHelper.SelectionModeAction;

        public SingleChoiceAction UserModeAction => _testControllerHelper.UserModeAction;

        public bool IsDebug => _testControllerHelper.ExecutionModeAction.SelectedItem.Caption == "Debug";

        private void UnlinkTestActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            var easyTests = e.SelectedObjects.Cast<EasyTest>().ToArray();
            OptionsProvider.Init(easyTests.Select(test => test.FileName).ToArray());
            if (ReferenceEquals(SelectionModeAction.SelectedItem.Data, TestControllerHelper.FromFile)) {
                var fileNames = File.ReadAllLines("easytests.txt").Where(s => !string.IsNullOrEmpty(s)).ToArray();
                easyTests = EasyTest.GetTests(ObjectSpace, fileNames);
            }
            foreach (var info in easyTests.SelectMany(test => test.GetCurrentSequenceInfos())) {
                info.WindowsUser = WindowsUser.CreateUsers((UnitOfWork)ObjectSpace.Session(), false).First();
                info.Setup(true);
            }
            ObjectSpace.RollbackSilent();

        }

        private void RunTestActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            var rdc = IsRDC();
            if (_runTestAction.Caption==CancelRun){
                if (_cancellationTokenSource != null) {
                    _cancellationTokenSource.Cancel();
                    var executionInfo = ObjectSpace.QueryObject<ExecutionInfo>(info=>info.Sequence==CurrentSequenceOperator.CurrentSequence);
                    var users = executionInfo.EasyTestRunningInfos.Select(info => info.WindowsUser.Name).Where(s => s!=null).ToArray();
                    EnviromentEx.LogOffAllUsers(users);
                    TestEnviroment.KillProcessAsUser();
                    ObjectSpace.Delete(executionInfo);
                    ObjectSpace.CommitChanges();
                }
                _runTestAction.Caption = Run;
            }
            else if (ReferenceEquals(SelectionModeAction.SelectedItem.Data, TestControllerHelper.Selected)) {
                _runTestAction.Caption = CancelRun;
                if (!rdc) {
                    _unlinkTestAction.DoExecute();
                }
                _cancellationTokenSource = TestRunner.Execute(e.SelectedObjects.Cast<EasyTest>().ToArray(), rdc,IsDebug,
                    task => _runTestAction.Caption = Run);}
            else {
                var fileName = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "EasyTests.txt");
                TestRunner.Execute(fileName, rdc);
            }
        }

        private bool IsRDC(){
            return SelectionModeAction.Active && ReferenceEquals(UserModeAction.SelectedItem.Data, TestControllerHelper.RDC);
        }

        public void ExtendModelInterfaces(ModelInterfaceExtenders extenders) {
            extenders.Add<IModelOptions, IModelOptionsTestExecutor>();
        }
    }
}