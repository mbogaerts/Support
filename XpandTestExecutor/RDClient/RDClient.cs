using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;

namespace RDClient {
    public partial class RDClient : Form {
        public RDClient() {
            InitializeComponent();
            Load += OnLoad;
            rdp.OnLoginComplete += RdpOnOnLoginComplete;
            rdp.OnLogonError += RdpOnOnLogonError;
        }

        private void RdpOnOnLogonError(object sender, IMsTscAxEvents_OnLogonErrorEvent e) {
            Trace.TraceError("LogonError=" + e.lError);
        }

        private void RdpOnOnLoginComplete(object sender, EventArgs eventArgs){
            Trace.TraceInformation("LoginComplete");
            Task task = Task.Factory.StartNew(() =>{
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                while (!RDSHelper.SessionExists(Options.Instance.UserName)){
                    Thread.Sleep(1000);
                    if (stopwatch.ElapsedMilliseconds < 5000)
                        break;
                }
            });
            Task.WaitAll(task);
        }

        private void OnLoad(object sender, EventArgs eventArgs) {
            var options = Options.Instance;
            Text = options.UserName;
            Connect(options.UserName,options.Password,Options.Instance.Domain);
        }

        public AxMsTscAxNotSafeForScripting Rdp {
            get { return rdp; }
        }

        public void Connect(string userName, string password, string domain=null) {
            rdp.DesktopWidth = 1440;
            rdp.DesktopHeight = 900;
            
            Width = rdp.DesktopWidth;
            Height = rdp.DesktopHeight;
            StartPosition=FormStartPosition.Manual;
            Location=new Point(1,0);
            rdp.Dock=DockStyle.Fill;
            rdp.BringToFront();
            rdp.Server = Environment.MachineName;
            if (domain != null)
                rdp.UserName = domain + @"\";
            rdp.UserName += userName;
            var secured = (IMsTscNonScriptable)rdp.GetOcx();
            secured.ClearTextPassword = password;
            rdp.Connect();
        }

        private void button1_Click(object sender, EventArgs e) {
            try {
                Connect(txtUserName.Text, txtPassword.Text);
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