using DevExpress.ExpressApp;
using Ploeh.AutoFixture;
using Xpand.Persistent.Base.General;

namespace Xpand.Test.AutoFixture.Customizations{
    public class ObjectSpaceCustomization : ICustomization{
        public void Customize(IFixture fixture){
            var objectSpace = ObjectSpaceInMemory.CreateNew();
            fixture.Inject(objectSpace);
            fixture.Inject(objectSpace.Session());
        }
    }
}