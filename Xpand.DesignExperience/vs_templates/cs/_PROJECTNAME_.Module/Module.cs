using DevExpress.ExpressApp.Security.Strategy;
using DevExpress.Persistent.BaseImpl;
using DevExpress.ExpressApp;
using System.Reflection;
using Xpand.ExpressApp.JobScheduler.Jobs.ThresholdCalculation;
using Xpand.ExpressApp.ModelDifference.Security;

namespace $projectsuffix$.Module {
    public sealed partial class $projectsuffix$Module : ModuleBase {
        public $projectsuffix$Module() {
            InitializeComponent();
			Xpand.Persistent.Base.General.SequenceGenerator.ThrowProviderSupportedException = false;
        }
		public override void Setup(ApplicationModulesManager moduleManager) {
            base.Setup(moduleManager);
            AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(typeof(Analysis)), IsExportedType));
            AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(typeof(Xpand.Persistent.BaseImpl.SequenceObject)), IsExportedType));
            AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(typeof(ThresholdSeverity)), IsExportedType));

        }

		
    }
}
