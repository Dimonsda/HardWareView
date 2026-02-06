using System;
using System.Drawing;
using System.Collections;
using System.Windows.Forms;
using MetroFramework.Forms;
using CircularProgressBar;
using System.Management;
using System.Threading;
using OpenHardwareMonitor;
using OpenHardwareMonitor.Hardware;
using SysInfo;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProcessorMonitor
{
    public partial class Form1 : MetroForm
    {
        SysInfo.SystemInfo info = new SystemInfo();
        public delegate void MyDelegate();

        private readonly Computer c = new Computer();
        private readonly Queue<int> cpuUsageHistory = new Queue<int>();
        private readonly Queue<float> ramUsageHistory = new Queue<float>();
        private const int HistoryWindow = 30;
        private bool hardwareMonitorInitialized;
        private float lastCpuTemperature;
        private float lastGpuTemperature;
        private int peakCpuUsage;
        private string detectedCpuName = string.Empty;
        private double detectedCpuTdpMax = 65;
        private MetroFramework.Controls.MetroLabel cpuTdpCurrentLabel;
        private MetroFramework.Controls.MetroLabel cpuTdpMaxLabel;
        public int CPUusage { get; set; }

        private sealed class CpuTdpProfile
        {
            public CpuTdpProfile(string pattern, string architecture, double maxTdp)
            {
                Pattern = pattern;
                Architecture = architecture;
                MaxTdp = maxTdp;
            }

            public string Pattern { get; private set; }
            public string Architecture { get; private set; }
            public double MaxTdp { get; private set; }
        }

        private static readonly List<CpuTdpProfile> KnownTdpProfiles = new List<CpuTdpProfile>
        {
            new CpuTdpProfile("intel.*ultra", "Intel Core Ultra", 115),
            new CpuTdpProfile("intel.*core.*i9", "Intel Core i9", 253),
            new CpuTdpProfile("intel.*core.*i7", "Intel Core i7", 190),
            new CpuTdpProfile("intel.*core.*i5", "Intel Core i5", 150),
            new CpuTdpProfile("intel.*core.*i3", "Intel Core i3", 90),
            new CpuTdpProfile("intel.*xeon", "Intel Xeon", 350),
            new CpuTdpProfile("intel.*celeron", "Intel Celeron", 58),
            new CpuTdpProfile("intel.*pentium", "Intel Pentium", 95),
            new CpuTdpProfile("amd.*ryzen.*9", "AMD Ryzen 9", 170),
            new CpuTdpProfile("amd.*ryzen.*7", "AMD Ryzen 7", 170),
            new CpuTdpProfile("amd.*ryzen.*5", "AMD Ryzen 5", 120),
            new CpuTdpProfile("amd.*ryzen.*3", "AMD Ryzen 3", 95),
            new CpuTdpProfile("amd.*threadripper", "AMD Threadripper", 350),
            new CpuTdpProfile("amd.*epyc", "AMD EPYC", 400),
            new CpuTdpProfile("amd.*athlon", "AMD Athlon", 95),
            new CpuTdpProfile("snapdragon|qualcomm", "ARM Snapdragon", 18),
            new CpuTdpProfile("apple.*m[1-4]", "Apple Silicon M", 60),
            new CpuTdpProfile("arm|cortex", "ARM Cortex", 45)
        };

        private void InitializeTdpLabels()
        {
            cpuTdpCurrentLabel = new MetroFramework.Controls.MetroLabel();
            cpuTdpCurrentLabel.AutoSize = true;
            cpuTdpCurrentLabel.FontWeight = MetroFramework.MetroLabelWeight.Regular;
            cpuTdpCurrentLabel.Location = new Point(13, 90);
            cpuTdpCurrentLabel.Text = "TDP (текущий): -- W";

            cpuTdpMaxLabel = new MetroFramework.Controls.MetroLabel();
            cpuTdpMaxLabel.AutoSize = true;
            cpuTdpMaxLabel.FontWeight = MetroFramework.MetroLabelWeight.Regular;
            cpuTdpMaxLabel.Location = new Point(13, 112);
            cpuTdpMaxLabel.Text = "TDP (макс): -- W";

            RAMTabPage.Controls.Add(cpuTdpCurrentLabel);
            RAMTabPage.Controls.Add(cpuTdpMaxLabel);
        }

        private double ResolveCpuMaxTdp(string cpuName)
        {
            var normalizedName = (cpuName ?? string.Empty).ToLowerInvariant();
            foreach (var profile in KnownTdpProfiles)
            {
                if (!Regex.IsMatch(normalizedName, profile.Pattern))
                {
                    continue;
                }

                return profile.MaxTdp;
            }

            return 95;
        }

        private string ResolveArchitectureName(string cpuName)
        {
            var normalizedName = (cpuName ?? string.Empty).ToLowerInvariant();
            foreach (var profile in KnownTdpProfiles)
            {
                if (Regex.IsMatch(normalizedName, profile.Pattern))
                {
                    return profile.Architecture;
                }
            }

            return "Unknown / mixed";
        }

        private void UpdateTdpView()
        {
            var currentTdp = Math.Round(detectedCpuTdpMax * CPUusage / 100.0, 1);
            cpuTdpCurrentLabel.Text = string.Format("TDP (текущий): {0:0.0} W", currentTdp);
            cpuTdpMaxLabel.Text = string.Format("TDP (макс): {0:0.#} W | {1}", detectedCpuTdpMax, ResolveArchitectureName(detectedCpuName));
        }

        private void EnsureHardwareMonitorInitialized()
        {
            if (hardwareMonitorInitialized)
            {
                return;
            }

            c.HDDEnabled = true;
            c.FanControllerEnabled = true;
            c.RAMEnabled = true;
            c.GPUEnabled = true;
            c.MainboardEnabled = true;
            c.CPUEnabled = true;
            c.Open();

            hardwareMonitorInitialized = true;
        }

        private static double AverageQueue(IEnumerable<int> source)
        {
            if (!source.Any())
            {
                return 0;
            }

            return source.Average();
        }

        private static double AverageQueue(IEnumerable<float> source)
        {
            if (!source.Any())
            {
                return 0;
            }

            return source.Average(x => (double)x);
        }

        private void UpdateHealthBadge()
        {
            var cpuTempWarning = lastCpuTemperature >= 80;
            var gpuTempWarning = lastGpuTemperature >= 80;
            var usageWarning = CPUusage >= 90;

            if (cpuTempWarning || gpuTempWarning || usageWarning)
            {
                Text = "HardwareView · Режим нагрузки 🔥";
                return;
            }

            Text = "HardwareView · Система стабильна ✅";
        }

        #region Hardware
        public void hddinfo()
        {
            try
            {
                ManagementObjectSearcher searcher =
                        new ManagementObjectSearcher("root\\Microsoft\\Windows\\Storage",
                        "SELECT * FROM MSFT_PhysicalDisk");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    hddlist.AppendText(string.Format("HDD: {0}", queryObj["FriendlyName"]) + Environment.NewLine + string.Format("Объём: {0}", Math.Truncate(Convert.ToDouble(queryObj["Size"]) / 1024 / 1024 / 1024) + " GB" + Environment.NewLine + Environment.NewLine));
                }
            }
            catch { }
        }
        public void usbinfo()
        {

            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_USBController");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                usb.AppendText(string.Format("{0}", queryObj["Name"] + Environment.NewLine + Environment.NewLine));
            }
        }
        public void proc()
        {
            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_Processor");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                //cpuname = queryObj["Name"].ToString();
                //cpusocket.Text = cpuname.Replace(" ", "");
                detectedCpuName = Convert.ToString(queryObj["Name"]);
                cpusocket.Text = string.Format("CPU: {0}", detectedCpuName);
                Socket.Text = string.Format("Сокет: {0}", queryObj["SocketDesignation"]);
                detectedCpuTdpMax = ResolveCpuMaxTdp(detectedCpuName);
                UpdateTdpView();
            }
        }
        public void cache()
        {
            List<string> result = new List<string>();
            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_CacheMemory");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                result.Add(queryObj["MaxCacheSize"].ToString() + " KB");
            }
            try
            {
                L1Cache.Text = "L1 " + result[0];
                L2Cache.Text = "L2 " + result[1];
                L3Cache.Text = "L3 " + result[2];
            }
            catch { }


        }
        public void Voltage()
        {
            EnsureHardwareMonitorInitialized();

            foreach (var hardware in c.Hardware)
            {
                // This will be in the mainboard
                foreach (var subhardware in hardware.SubHardware)
                {
                    // This will be in the SuperIO
                    subhardware.Update();
                                    if (subhardware.Sensors.Length > 0) // Index out of bounds check
                                    {
                                        voltagebox.Clear();
                                        foreach (var sensor in subhardware.Sensors)
                                        {
                            // Look for the main fan sensor
                            if (sensor.SensorType == SensorType.Voltage)
                            {

                                voltagebox.AppendText(sensor.Name + "      " + sensor.Value + " V" + Environment.NewLine);
                            }
                        }
                    }
                }
            }
        }
        public void Hardware()
        {
            try
            {

                List<double> cpulist = new List<double>();
                List<string> gpuprop = new List<string>();
                List<string> gpulist = new List<string>();


                EnsureHardwareMonitorInitialized();

                foreach (var hardware in c.Hardware)
                {
                    switch (hardware.HardwareType)
                    {
                        case HardwareType.CPU:
                            hardware.Update();
                            foreach (var sensors in hardware.Sensors)
                            {
                                if (sensors.SensorType == SensorType.Temperature)
                                {
                                    tempcpu.Text = (sensors.Value.GetValueOrDefault() + "°C");
                                    circularProgressBar3.Value = (int)sensors.Value;
                                    lastCpuTemperature = sensors.Value.GetValueOrDefault();
                                }
                                if (sensors.SensorType == SensorType.Clock)
                                {
                                    cpulist.Add(Convert.ToDouble(sensors.Value.GetValueOrDefault().ToString()));
                                }
                                if (sensors.SensorType == SensorType.Load)
                                {
                                    CPUusage = (int)sensors.Value.GetValueOrDefault();
                                }
                            }
                            try
                            {
                                cpuclock.Text = Math.Truncate(cpulist[0]).ToString() + " MHz";
                                var rnclock = cpulist[0] / (cpulist[cpulist.Count - 1]);
                                var clock = Math.Round(rnclock);
                                Multipl.Text = "Множитель: " + clock;
                            }
                            catch { }
                            break;

                        case HardwareType.GpuNvidia:

                            hardware.Update();

                            if (hardware.HardwareType == HardwareType.GpuNvidia)
                            {
                                foreach (var sensors in hardware.Sensors)
                                {
                                    if (sensors.SensorType == SensorType.Fan)
                                    {
                                        gpufanspeed.Text = (sensors.Value.GetValueOrDefault() + "RPM");
                                    }
                                    if (sensors.SensorType == SensorType.Clock)
                                    {
                                        gpuprop.Add(sensors.Name + ": " + Math.Round(Convert.ToDouble(sensors.Value.GetValueOrDefault())));
                                        try
                                        {
                                            {
                                                gpucore.Text = (gpuprop[0] + " MHz");
                                                gpushader.Text = (gpuprop[2] + " MHz");
                                                gpumemory.Text = (gpuprop[1] + " MHz");
                                            }
                                        }
                                        catch { }
                                    }
                                    if (sensors.SensorType == SensorType.Temperature)
                                    {
                                        try
                                        {
                                            tempgpu.Text = (sensors.Value.GetValueOrDefault() + "°C");
                                            lastGpuTemperature = sensors.Value.GetValueOrDefault();
                                        }
                                        catch { }
                                        if (sensors.Max.GetValueOrDefault() > 60)
                                        {
                                            circularProgressBar3.ProgressColor = Color.Red;
                                        }
                                    }


                                }
                            }
                            break;

                        case HardwareType.GpuAti:
                            hardware.Update();
                            if (hardware.HardwareType == HardwareType.GpuAti)
                            {

                                gpulist.Add(hardware.Name);
                                foreach (var sensors in hardware.Sensors)
                                {
                                    if (sensors.SensorType == SensorType.Fan)
                                    {
                                        gpufanspeed.Text = (sensors.Value.GetValueOrDefault() + "RPM");
                                    }
                                    if (sensors.SensorType == SensorType.Clock)
                                    {
                                        gpuprop.Add(sensors.Name + ": " + Math.Round(Convert.ToDouble(sensors.Value.GetValueOrDefault())));

                                        try
                                        {
                                            gpucore.Text = (gpuprop[0] + " MHz");
                                            gpushader.Text = (gpuprop[2] + " MHz");
                                            gpumemory.Text = (gpuprop[1] + " MHz");
                                            gpuname.Text = (string.Join(" + ", gpulist));
                                        }
                                        catch { }
                                    }
                                }
                            }
                            break;

                        case HardwareType.HDD:
                            break;

                        case HardwareType.Mainboard:

                            foreach (var subhardware in hardware.SubHardware)
                            {
                                // This will be in the SuperIO
                                subhardware.Update();
                                if (subhardware.Sensors.Length > 0) // Index out of bounds check
                                {
                                    foreach (var sensor in subhardware.Sensors)
                                    {
                                        // Look for the main fan sensor
                                        if (sensor.SensorType == SensorType.Fan)
                                        {
                                            CpuRPM.Text = ("CPU Fan Speed: " + Math.Round(Convert.ToDouble(sensor.Value.GetValueOrDefault())) + " RPM");
                                        }
                                    }
                                }
                            }
                            break;

                        case HardwareType.RAM:
                            break;
                    }
                }
                //if (gpuprop == null)
                //{
                //    gpucore.Visible = false;
                //    gpushader.Visible = false;
                //    gpumemory.Visible = false;
                //}
            }
            catch { }
        }
        public void Test()
        {
           
        }
        public void readclass()
        {
            info.ports();
            info.videocard();
            info.motherboard();
            info.videoRAM();
            info.network();
            info.wininfo();
            info.totalRam();
            info.raminfo();



            PCname.Text = info.OsName;

            gpuname.Text = info.GPUname;
            gpuramsize.Text = info.GPUSize;
            version.Text = info.Version;


            //rambox.AppendText(info.RamModule + info.RamValue);
            rambox.AppendText(string.Join("",info.raminfor));



            Ports.AppendText(string.Join("", info.portss));

            Motherboard.Text = (info.Motherboard + Environment.NewLine + info.MotherboardSerial);

            Network.AppendText(string.Join(Environment.NewLine + Environment.NewLine, info.networks));



            osname.Text = info.OsName;

            MonitorSize.Text = info.MonitorSize;

            Corecount.Text = info.CoreCount;

            osnumber.Text = info.CoreNumber;

            PCname.Text = info.PCname;

            allram.Text = (new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory/1024/1024).ToString() + " MB";

            totalram.Text = info.TotalRam;
        }
        public void wininfo()
        {
            //HDD
            HDD.Text = "Свободно: " + (Hddinfo1.NextValue()).ToString("00.##") + "%";
        }
        #endregion

        public Form1()
        {
            InitializeComponent();
            InitializeTdpLabels();
            EnsureHardwareMonitorInitialized();
            //Потоки
            backgroundWorker1.RunWorkerAsync();
            backgroundWorker2.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            BeginInvoke(new MyDelegate(Hardware));
            BeginInvoke(new MyDelegate(Voltage));
            backgroundWorker1.CancelAsync();
        }

        private void backgroundWorker2_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            BeginInvoke(new MyDelegate(readclass));
            BeginInvoke(new MyDelegate(hddinfo));
            BeginInvoke(new MyDelegate(cache));
            BeginInvoke(new MyDelegate(proc));
            BeginInvoke(new MyDelegate(Test));
            BeginInvoke(new MyDelegate(wininfo));
            BeginInvoke(new MyDelegate(usbinfo));

            backgroundWorker2.CancelAsync();
        }


        public void timer1_Tick(object sender, EventArgs e)
        {

            loading.Enabled = false;
            loading.Visible = false;
            info.time();
            time.Text = info.Time;

            //Проц и оператива
            circularProgressBar1.Value = CPUusage;
            cpuUsageHistory.Enqueue(circularProgressBar1.Value);
            if (cpuUsageHistory.Count > HistoryWindow)
            {
                cpuUsageHistory.Dequeue();
            }

            peakCpuUsage = Math.Max(peakCpuUsage, circularProgressBar1.Value);
            cpuusage.Text = string.Format("{0}% | avg {1:0}% | peak {2}%", circularProgressBar1.Value, AverageQueue(cpuUsageHistory), peakCpuUsage);

            lblMemoryAvailable.Text = ((int)pcMemoryAvailable.NextValue()).ToString() + " MB";
            var currentRamUsage = Memory.NextValue();
            circularProgressBar2.Value = (int)currentRamUsage;
            ramUsageHistory.Enqueue(currentRamUsage);
            if (ramUsageHistory.Count > HistoryWindow)
            {
                ramUsageHistory.Dequeue();
            }

            ramproc.Text = string.Format("{0}% | avg {1:0}%", Math.Truncate(currentRamUsage), AverageQueue(ramUsageHistory));

            HDDspeed.Text = (Hddinfo2.NextValue() / 1024 / 1024).ToString("0.###") + " MB/s";
            UpdateTdpView();
            UpdateHealthBadge();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            Voltage();
            Hardware();
        }
    }
}
