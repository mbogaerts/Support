using System.Diagnostics;
using System.Security.Principal;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;

namespace XpandTestExecutor.Module.Controllers {
    public class TestControllerHelper:WindowController {
        private readonly ParametrizedAction _executionRetriesAction;

        internal const string Selected = "Selected";
        internal const string FromFile = "FromFile";
        internal const string RDC = "RDC";
        private const string Current = "Current";
        private readonly SingleChoiceAction _userModeAction;
        private readonly SingleChoiceAction _selectionModeAction;
        private readonly SingleChoiceAction _executionModeAction;


        public TestControllerHelper() {
            TargetWindowType=WindowType.Main;

            _userModeAction = new SingleChoiceAction(this, "UserMode", PredefinedCategory.Tools) { Caption = "Identity" };
            _userModeAction.Items.Add(new ChoiceActionItem(RDC, RDC));
            _userModeAction.Items.Add(new ChoiceActionItem(Current, Current));

            _selectionModeAction = new SingleChoiceAction(this, "SelectionMode", PredefinedCategory.Tools) { Caption = "Selection" };
            _selectionModeAction.Items.Add(new ChoiceActionItem(FromFile, FromFile));
            _selectionModeAction.Items.Add(new ChoiceActionItem(Selected, Selected));

            _executionRetriesAction = new ParametrizedAction(this, "ExecutionRetries", PredefinedCategory.Tools, typeof(int)){Caption = "Retries"};
            _executionRetriesAction.Execute += ExecutionRetriesActionOnExecute;

            _executionModeAction = new SingleChoiceAction(this, "ExecutionMode", PredefinedCategory.Tools) { Caption = "Execution" };
            _executionModeAction.Execute += ExecutionModeActionOnExecute;
            _executionModeAction.Items.Add(new ChoiceActionItem("Normal", null));
            _executionModeAction.Items.Add(new ChoiceActionItem("Debug", null));
        }

        private void ExecutionModeActionOnExecute(object sender, SingleChoiceActionExecuteEventArgs e) {

        }

        public SingleChoiceAction ExecutionModeAction {
            get { return _executionModeAction; }
        }

        public SingleChoiceAction UserModeAction {
            get { return _userModeAction; }
        }

        private void ExecutionRetriesActionOnExecute(object sender, ParametrizedActionExecuteEventArgs parametrizedActionExecuteEventArgs) {
            ((IModelOptionsTestExecutor)Application.Model.Options).ExecutionRetries = (int)parametrizedActionExecuteEventArgs.ParameterCurrentValue;
        }
        protected override void OnActivated() {
            base.OnActivated();
            _executionRetriesAction.Value = ((IModelOptionsTestExecutor)Application.Model.Options).ExecutionRetries;
        }
        public SingleChoiceAction SelectionModeAction {
            get { return _selectionModeAction; }
        }
    }
}