
Imports DevExpress.ExpressApp
Imports System.Reflection
Imports DevExpress.Persistent.BaseImpl
Imports Xpand.ExpressApp.JobScheduler.Jobs.ThresholdCalculation

Partial Public NotInheritable Class [$projectsuffix$Module]
    Inherits ModuleBase
    Public Sub New()
        InitializeComponent()
		Xpand.Persistent.Base.General.SequenceGenerator.ThrowProviderSupportedException = False;
    End Sub

	Public Overrides Sub Setup(ByVal moduleManager As ApplicationModulesManager)
        MyBase.Setup(moduleManager)
        AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(GetType(Analysis)), AddressOf IsExportedType))
        AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(GetType(Xpand.Persistent.BaseImpl.SequenceObject)), AddressOf IsExportedType))
        AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(GetType(ThresholdSeverity)), AddressOf IsExportedType))
    End Sub

End Class
