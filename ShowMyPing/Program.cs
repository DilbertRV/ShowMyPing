using Microsoft.Win32;
using ShowMyPing.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShowMyPing
{
    public class Program
    {
        [STAThread]
        static void Main()
        {
            CultureInfo.DefaultThreadCurrentUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ShowMyPingApplicationContext());
        }
    }

    public class ShowMyPingApplicationContext : ApplicationContext
    {
        CustomAddressForm form = new CustomAddressForm();

        private string ping = UserSettings.Default.pingUser;
        private string averagePing;
        List<string> pingData = new List<string>();
        List<int> pingDataInt = new List<int>();
        List<PingReply> listaNueva = new List<PingReply>();

        private ToolStripMenuItem defaultAddress;
        private ToolStripMenuItem customAddress;
        private ToolStripMenuItem startupMenu;
        private NotifyIcon notifyIcon;
        private int current = 0;

        private Timer pingTimer = new Timer();

        public ShowMyPingApplicationContext()
        {
            Application.ApplicationExit += new EventHandler(OnApplicationExit);

            defaultAddress = new ToolStripMenuItem("Default Address", null, SetDefault);
            if (IsDefaultEnabled())
            {
                defaultAddress.Checked = true;
            }

            customAddress = new ToolStripMenuItem("Custom Address", null, OpenCustomAddressForm);

            startupMenu = new ToolStripMenuItem("Startup", null, SetStartup);
            if (IsStartupEnabled())
            {
                startupMenu.Checked = true;
            }

            ContextMenuStrip contextMenuStrip = new ContextMenuStrip(new Container());
            contextMenuStrip.Items.AddRange(new ToolStripItem[]
            {
                defaultAddress,
                customAddress,
                startupMenu,
                new ToolStripMenuItem("Exit", null, Exit)
            });

            notifyIcon = new NotifyIcon()
            {
                Icon = Resources.ico_ping_light,
                ContextMenuStrip = contextMenuStrip,
                Text = "Show My Ping",
                Visible = true
            };

            VerifyPingHost();
            StartPingTest();
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            UserSettings.Default.pingUser = ping;
            UserSettings.Default.Save();
        }
        public void VerifyPingHost()
        {
            if (IsDefaultEnabled())
                defaultAddress.Checked = true;
            else
                customAddress.Checked = true;
            notifyIcon.Text = "Show My Ping" + "\n" + ping;
        }

        private bool IsDefaultEnabled()
        {
            return ping == "8.8.8.8" ? true : false;
        }

        private void SetDefault(object sender, EventArgs e)
        {
            if (!IsDefaultEnabled())
               defaultAddress.Checked = !defaultAddress.Checked;
            if (defaultAddress.Checked)
            {
                form.customAddress = null;
                ping = "8.8.8.8";
                customAddress.Checked = false;
            }
            notifyIcon.Text = "Show My Ping" + "\n" + ping;
        }

        private bool IsStartupEnabled()
        {
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName))
            {
                return (rKey.GetValue(Application.ProductName) != null) ? true : false;
            }
        }

        private void SetStartup(object sender, EventArgs e)
        {
            startupMenu.Checked = !startupMenu.Checked;
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName, true))
            {
                if (startupMenu.Checked)
                {
                    rKey.SetValue(Application.ProductName, Process.GetCurrentProcess().MainModule.FileName);
                }
                else
                {
                    rKey.DeleteValue(Application.ProductName, false);
                }
                rKey.Close();
            }
        }

        private void OpenCustomAddressForm(object sender, EventArgs e)
        {
            defaultAddress.Checked = false;
            customAddress.Enabled = false;
            form.ShowDialog();
            if (form.customAddress != null)
            {
                ping = form.customAddress;
                customAddress.Checked = true;
            }
            else
                defaultAddress.Checked = true;
            customAddress.Enabled = true;
            notifyIcon.Text = "Show My Ping" + "\n" + ping;
        }

            private void Exit(object sender, EventArgs e)
        {
            pingTimer.Stop();
            notifyIcon.Visible = false;
            Application.Exit();
        }

        private void ShowLatency(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                StartObservePing();
        }
        private void StartPingTest()
        {
            notifyIcon.MouseClick += ShowLatency;
        }

        private async Task<List<PingReply>> PingAsync()
        {

            Ping pingClass = new Ping();

            var tasks = pingClass.SendPingAsync(ping, 2000);

            var results = await Task.WhenAll(tasks);

            current++;

            return results.ToList();
        }  

        public void CalculatingAverage()
        {
            int sum = 0;

            foreach (int ping in pingDataInt)
            {
                sum += ping;
            }
            averagePing = (sum / 4).ToString() + "ms";
        }

        private async void ObservePingTick(object sender, EventArgs e)
        {
            try
            {
                await FillListWithPingInformation();
            }
            catch (PingException p)
            {
                pingTimer.Stop();
                pingTimer.Tick -= ObservePingTick;
                current = 0;
                MessageBox.Show("\nAn error occurred, the application will be restarted. " +
                    "If the error persists please create an issue on the project page on GitHub." + "\nAddress: " + ping, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (MessageBox.Show("¿Would you like to be redirected to the repository of this project and create an issue?", "Visit", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                {
                    Process.Start("https://github.com/DilbertRV/ShowMyPing/issues");
                }
                Application.Restart();
            }
        }

        public async Task FillListWithPingInformation()
        {
            if (!(current == 4))
            {
                Task<List<PingReply>> longRunningTask = PingAsync();
                listaNueva = await longRunningTask;
                foreach (PingReply ping in listaNueva)
                {

                    if (ping.Status == IPStatus.Success)
                    {
                        pingData.Add(ping.RoundtripTime.ToString() + "ms");
                        pingDataInt.Add((int)ping.RoundtripTime);
                    }
                    else
                    {
                        if (ping.Status == IPStatus.TimedOut)
                        {
                            pingData.Add("Timeout");
                            pingDataInt.Add(((int)ping.RoundtripTime));
                        }
                        else if (ping.Status == IPStatus.TimeExceeded)
                        {
                            pingData.Add("Time Exceeded");
                            pingDataInt.Add(((int)ping.RoundtripTime));
                        }
                        else if (ping.Status == IPStatus.DestinationHostUnreachable)
                        {
                            pingData.Add("Host Unreachable");
                            pingDataInt.Add(((int)ping.RoundtripTime));
                        }
                        else if (ping.Status == IPStatus.DestinationNetworkUnreachable)
                        {
                            pingData.Add("Network Error");
                            pingDataInt.Add(((int)ping.RoundtripTime));
                        }
                    }  
                }
            }
            else
            {
                pingTimer.Stop();
                pingTimer.Tick -= ObservePingTick;
                current = 0;

                int count = 0;
                for (int i = 0; i < pingData.Count; i++)
                {
                    if (!(pingData[i].Contains("ms")))
                    {
                        count++;
                    }
                }
                if (!(count == 4))
                {
                    if (count > 0)
                    {
                        CalculatingAverage();
                        string Data = string.Join(" - ", pingData);
                        notifyIcon.BalloonTipTitle = "Pinging " + ping;
                        notifyIcon.BalloonTipText = "Ping: " + Data + "\n" + "Average: " + averagePing + " * with packet loss or connection problems.";
                        notifyIcon.Text = "Show My Ping" + "\n" + ping;
                        notifyIcon.ShowBalloonTip(5000);
                        pingData.Clear();
                        pingDataInt.Clear();
                    }
                    else
                    {
                        CalculatingAverage();
                        string Data = string.Join(" - ", pingData);
                        notifyIcon.BalloonTipTitle = "Pinging " + ping;
                        notifyIcon.BalloonTipText = "Ping: " + Data + "\n" + "Average: " + averagePing;
                        notifyIcon.Text = "Show My Ping" + "\n" + ping;
                        notifyIcon.ShowBalloonTip(5000);
                        pingData.Clear();
                        pingDataInt.Clear();
                    }   
                }
                else
                {
                    string Data = string.Join(" - ", pingData);
                    notifyIcon.BalloonTipTitle = "Pinging " + ping;
                    notifyIcon.BalloonTipText = "Ping: " + Data + "\n" + "There was no response or there is a problem with your connection: \n" 
                        + "Read the ping responses to see the problem.";
                    notifyIcon.Text = "Show My Ping" + "\n" + ping;
                    notifyIcon.ShowBalloonTip(5000);
                    pingData.Clear();
                    pingDataInt.Clear();
                }                
            }
        }

        private void StartObservePing() 
        {
            pingTimer.Interval = 1100;
            pingTimer.Tick += new EventHandler(ObservePingTick);
            pingTimer.Start();
        }
    }
}
