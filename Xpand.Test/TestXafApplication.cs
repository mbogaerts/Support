using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Layout;
using DevExpress.ExpressApp.Xpo;

namespace Xpand.Test {
    public class TestXafApplication:XafApplication {
        private readonly LayoutManager _layoutManager;

        public TestXafApplication(LayoutManager layoutManager){
            _layoutManager = layoutManager;
            DatabaseVersionMismatch+=OnDatabaseVersionMismatch;
        }

        private void OnDatabaseVersionMismatch(object sender, DatabaseVersionMismatchEventArgs e){
            e.Updater.Update();
            e.Handled = true;
        }

        protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args){
            args.ObjectSpaceProvider = new XPObjectSpaceProvider(new MemoryDataStoreProvider());
        }

        protected override LayoutManager CreateLayoutManagerCore(bool simple){
            return _layoutManager;
        }
    }

    public class TestLayoutManager:LayoutManager{
        protected override object GetContainerCore(){
            return null;
        }
    }
}
