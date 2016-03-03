using System.Linq;
using DevExpress.ExpressApp;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.Controllers.Dashboard;
using XpandTestExecutor.Module.BusinessObjects;
using XpandTestExecutor.Module.Services;

namespace XpandTestExecutor.Module.Controllers {
    public class EasyTestDashBoardController : ViewController<DashboardView> {
        protected override void OnActivated() {
            base.OnActivated();
            Frame.GetController<DashboardInteractionController>().ListViewFiltering += OnListViewFiltering;
        }

        protected override void OnDeactivated() {
            base.OnDeactivated();
            Frame.GetController<DashboardInteractionController>().ListViewFiltering -= OnListViewFiltering;
        }

        private void OnListViewFiltering(object sender, ListViewFilteringArgs listViewFilteringArgs) {
            listViewFilteringArgs.Handled = true;
            var executionInfo = listViewFilteringArgs.DataSourceListView.SelectedObjects.Cast<ExecutionInfo>().FirstOrDefault();
            if (executionInfo != null) {
                CurrentSequenceOperator.CurrentSequence = executionInfo.Sequence;
                var listView = ((ListView)listViewFilteringArgs.DashboardViewItem.Frame.View);

                var expression = ObjectSpace.TransformExpression<EasyTest>(test => test.EasyTestExecutionInfos.Any(info => info.ExecutionInfo.Sequence == CurrentSequenceOperator.CurrentSequence));
                listView.CollectionSource.Criteria["currentsequence"] = expression;
            }
        }
    }
}
