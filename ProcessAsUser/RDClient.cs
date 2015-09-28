using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;

namespace ProcessAsUser {
    public partial class RDClient : Form {
        private readonly ProcessAsUser _processAsUser;
        private CancellationTokenSource _cancellationTokenSource;

        public RDClient() {
            InitializeComponent();
        }

        public RDClient(ProcessAsUser processAsUser):this(){
            _processAsUser = processAsUser;
            Load += OnLoad;
            rdp.OnLoginComplete+=RdpOnOnLoginComplete;
            rdp.OnLogonError += RdpOnOnLogonError;
        }

        private void RdpOnOnLogonError(object sender, IMsTscAxEvents_OnLogonErrorEvent e) {
            Program.Logger.Error("LogonError=" + e.lError);
        }

        private void CancelWaitForExit(){
            if (_cancellationTokenSource != null) _cancellationTokenSource.Cancel();
        }

        private void RdpOnOnLoginComplete(object sender, EventArgs eventArgs){
            Program.Logger.Info("LoginComplete");
            Task task = Task.Factory.StartNew(() =>{
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                while (!_processAsUser.SessionExists()){
                    Thread.Sleep(1000);
                    if (stopwatch.ElapsedMilliseconds < 5000)
                        break;
                }
            });
            Task.WaitAll(task);
            var createProcess = _processAsUser.CreateProcess();
            Program.Logger.Info("CreateProcess="+createProcess);
            CancelWaitForExit();
            Close();
        }

        private void OnLoad(object sender, EventArgs eventArgs) {
            Text = _processAsUser.Options.Arguments;
            bool sessionExists = _processAsUser.SessionExists();
            Program.Logger.Info("SessionExists=" + sessionExists);
            if (!sessionExists)
                Connect(_processAsUser.Options.UserName, _processAsUser.Options.Password,_processAsUser.Options.Domain);
            else{
                _processAsUser.CreateProcess();
                Close();
            }
        }

        public AxMsTscAxNotSafeForScripting Rdp {
            get { return rdp; }
        }

        public void Connect(string userName, string password, string domain) {
            rdp.DesktopWidth = 1440;
            rdp.DesktopHeight = 900;
            
            Width = rdp.DesktopWidth;
            Height = rdp.DesktopHeight;
            StartPosition=FormStartPosition.Manual;
            Location=new Point(1,0);
            rdp.Dock=DockStyle.Fill;
            rdp.BringToFront();
            rdp.Server = Environment.MachineName;
            if (!string.IsNullOrEmpty(domain))
                rdp.UserName = domain + @"\";
            rdp.UserName += userName;
            var secured = (IMsTscNonScriptable)rdp.GetOcx();
            secured.ClearTextPassword = password;
            rdp.Connect();
            _cancellationTokenSource = Program.ExitOnTimeout(_processAsUser.Options);
        }

        private void button1_Click(object sender, EventArgs e) {
            try {
                Connect(txtUserName.Text, txtPassword.Text,null);
            }
            catch (Exception ex) {
                MessageBox.Show("Error Connecting",
                    "Error connecting to remote desktop " + txtServer.Text + " Error:  " + ex.Message,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            try {
                if (rdp.Connected.ToString(CultureInfo.InvariantCulture) == "1")
                    rdp.Disconnect();
            }
            catch (Exception ex) {
                MessageBox.Show("Error Disconnecting",
                    "Error disconnecting from remote desktop " + txtServer.Text + " Error:  " + ex.Message,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}