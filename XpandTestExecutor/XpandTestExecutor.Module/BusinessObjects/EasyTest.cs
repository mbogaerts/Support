using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Xml.Serialization;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;
using XpandTestExecutor.Module.Services;

namespace XpandTestExecutor.Module.BusinessObjects {
    [DefaultClassOptions]
    [DefaultProperty("Name")]
    public class EasyTest : BaseObject, ISupportSequenceObject {
        private string _application;
        private EasyTestExecutionInfo _lastEasyTestExecutionInfo;

        public EasyTest(Session session)
            : base(session) {
        }

        public LogTest[] GetLogTests() {
            var directoryName = Path.GetDirectoryName(FileName) + "";
            var fileName = Path.Combine(directoryName, "testslog.xml");
            if (File.Exists(fileName)) {
                using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    return LogTests.LoadTestsResults(optionsStream).Tests.Where(test => test != null&&test.Result!="Ignored").ToArray();
                }
            }
            return new LogTest[0];
        }

        public override string ToString() {
            return Application + " " + Name;
        }

        [Obsolete(ObsoleteMessage.DontUseFromCode,true)]
        public double Duration => GetCurrentSequenceInfos().Duration();

        [InvisibleInAllViews]
        [Obsolete(ObsoleteMessage.DontUseFromCode,true)]
        public XPCollection<EasyTestExecutionInfo> FailedEasyTestExecutionInfos => GetCurrentSequenceInfos().Failed();


        public XPCollection<EasyTestExecutionInfo> GetCurrentSequenceInfos() {
            return new XPCollection<EasyTestExecutionInfo>(Session,
                EasyTestExecutionInfos.Where(info => info.ExecutionInfo.Sequence == CurrentSequenceOperator.CurrentSequence));
        }

        [InvisibleInAllViews]
        [Obsolete(ObsoleteMessage.DontUseFromCode,true)]
        public bool Failed{
            get {
                return GetCurrentSequenceInfos().All(info => info.State == EasyTestState.Failed || info.State == EasyTestState.Running);
            }
        }

        [InvisibleInAllViews]
        [Obsolete(ObsoleteMessage.DontUseFromCode,true)]
        public bool Executed{
            get{
                var easyTestExecutionInfos = GetCurrentSequenceInfos();
                return easyTestExecutionInfos.Count>1|| easyTestExecutionInfos.All(info => info.State == EasyTestState.NotStarted);
            }
        }

        [InvisibleInAllViews]
        [Obsolete(ObsoleteMessage.DontUseFromCode,true)]
        public bool Passed{
            get{
                var easyTestExecutionInfos = GetCurrentSequenceInfos();
                return easyTestExecutionInfos.Any(info => info.State == EasyTestState.Passed);
            }
        }

        [InvisibleInAllViews]
        [Obsolete(ObsoleteMessage.DontUseFromCode,true)]
        public bool Running{
            get{
                var easyTestExecutionInfos = GetCurrentSequenceInfos();
                return easyTestExecutionInfos.Count(info => info.State == EasyTestState.Running) == 1 &&
                       easyTestExecutionInfos.All(info => info.State != EasyTestState.Passed) &&
                       easyTestExecutionInfos.Select(info => info.ExecutionInfo).Distinct()
                           .Any(info => info.FailedTests.Contains(this));
            }
        }


        [Association("EasyTestExecutionInfo-EasyTests")]
        public XPCollection<EasyTestExecutionInfo> EasyTestExecutionInfos => GetCollection<EasyTestExecutionInfo>("EasyTestExecutionInfos");

        [Size(SizeAttribute.Unlimited)]
        [RuleUniqueValue]
        [ModelDefault("RowCount", "1")]
        public string FileName { get; set; }

        [VisibleInDetailView(false)]
        [RuleRequiredField]
        public string Application {
            get { return _application; }
            set { SetPropertyValue("Application", ref _application, value); }
        }

        [VisibleInDetailView(false)]
        public string Name => Path.GetFileNameWithoutExtension(FileName);

        [InvisibleInAllViews]
        public EasyTestExecutionInfo LastEasyTestExecutionInfo => _lastEasyTestExecutionInfo ?? GetLastInfo();

        [InvisibleInAllViews]
        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix => null;

        [Browsable(false)]
        public Options Options => OptionsProvider.Instance[FileName];

        public void CreateExecutionInfo(bool useCustomPort, ExecutionInfo executionInfo, WindowsUser windowsUser = null) {
            _lastEasyTestExecutionInfo = new EasyTestExecutionInfo(Session) {
                ExecutionInfo = executionInfo,
                EasyTest = this,
                WinPort = 4100,
                WebPort = 4030,
                WindowsUser = windowsUser,
            };
            _lastEasyTestExecutionInfo.CreateApplications(FileName);
            if (useCustomPort) {
                IQueryable<EasyTestExecutionInfo> executionInfos =
                    new XPQuery<EasyTestExecutionInfo>(Session, true).Where(
                        info => info.ExecutionInfo.Oid == executionInfo.Oid);
                int winPort = executionInfos.Max(info => info.WinPort);
                int webPort = executionInfos.Max(info => info.WebPort);
                _lastEasyTestExecutionInfo.WinPort = winPort + 1;
                _lastEasyTestExecutionInfo.WebPort = webPort + 1;
            }
            EasyTestExecutionInfos.Add(_lastEasyTestExecutionInfo);
        }

        private EasyTestExecutionInfo GetLastInfo() {
            if (EasyTestExecutionInfos.Any()) {
                long max = EasyTestExecutionInfos.Max(info => info.Sequence);
                return EasyTestExecutionInfos.First(info => info.Sequence == max);
            }
            return null;
        }

        protected override void OnSaving() {
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }

        public static EasyTest[] GetTests(IObjectSpace objectSpace, string[] fileNames) {
            var easyTests = new EasyTest[fileNames.Length];
            for (int index = 0; index < fileNames.Length; index++) {
                var fileName = fileNames[index];
                string name = fileName;
                var easyTest = objectSpace.QueryObject<EasyTest>(test => test.FileName == name) ?? objectSpace.CreateObject<EasyTest>();
                easyTest.FileName = fileName;
                easyTest.Application = easyTest.Options.Applications.Cast<TestApplication>().Select(application => application.Name.Replace(".Win", "").Replace(".Web", "")).First();
                easyTests[index] = easyTest;
            }
            var array = easyTests.OrderByDescending(test => test.LastPassedDuration()).ToArray();
            return array;
        }

        public int LastPassedDuration() {
            var executionInfo = GetLastPassedInfo();
            if (executionInfo != null)
                return executionInfo.EasyTestExecutionInfos.Where(info => info.EasyTest.Oid == Oid).Duration();
            return 0;
        }

        public ExecutionInfo GetLastPassedInfo() {
            return new XPQuery<ExecutionInfo>(Session).FirstOrDefault(info => info.EasyTestExecutionInfos.Any(executionInfo => executionInfo.EasyTest.Oid == Oid && executionInfo.State == EasyTestState.Passed));
        }

        public void SerializeOptions(string userName) {
            string configPath = Path.GetDirectoryName(FileName) + "";
            string fileName = Path.Combine(configPath, "config.xml");
            File.Delete(fileName);
            while (File.Exists(fileName)){
                Thread.Sleep(100);
            }
            using (var fileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)) {
                new XmlSerializer(typeof(Options)).Serialize(fileStream, Options);
            }
            var document = XDocument.Load(fileName);
            var applicationElement =
                document.Descendants("Application").FirstOrDefault(element =>
                            element.Attributes("PhysicalPath").Any() &&
                            element.Attributes("DontUseIIS").All(attribute => attribute.Value != "True"));
            if (applicationElement != null){
                applicationElement.SetAttributeValue("UseIIS", "True");
                applicationElement.SetAttributeValue("UserName",userName);
                document.Save(fileName);
            }
            
        }
    }
}