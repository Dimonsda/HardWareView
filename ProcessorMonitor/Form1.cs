﻿using System;
using System.Drawing;
using System.Windows.Forms;
using MetroFramework.Forms;
using CircularProgressBar;
using System.Management;
using System.Threading;
using OpenHardwareMonitor.Hardware;

namespace ProcessorMonitor
{
    public partial class Form1 : MetroForm
    {
        public delegate void MyDelegate();
        public void hddinfo()
        {

            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\Microsoft\\Windows\\Storage",
                    "SELECT * FROM MSFT_PhysicalDisk");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                hddlist.AppendText(string.Format("HDD: {0}", queryObj["FriendlyName"]) + Environment.NewLine + string.Format("Объём: {0}", Math.Truncate(Convert.ToDouble(queryObj["Size"]) / 1024 / 1024 / 1024) + " GB" + Environment.NewLine));
            }

        }
        public void raminfo2()
        {

            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_PhysicalMemoryArray");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                raznoe.AppendText(string.Format("Максимальный объём: {0}", Convert.ToDouble(queryObj["MaxCapacity"]) / 1024) + "MB" + Environment.NewLine);
                raznoe.AppendText("-----------------------------------------" + Environment.NewLine);
                //raznoe.AppendText(string.Format("Модулей памяти: {0}", queryObj["Use"]) + Environment.NewLine);
            }
        }
        public void raminfo()
        {

            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_PhysicalMemory");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                raznoe.AppendText(string.Format("Модуль: {0}", queryObj["DeviceLocator"]) + Environment.NewLine);
                raznoe.AppendText(string.Format("Объём: {0}", Convert.ToDouble(queryObj["Capacity"]) / 1024 / 1024) + " MB" + Environment.NewLine);
            }
        }
        public void usbinfo()
        {

            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_USBController");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                usb.AppendText(string.Format("{0}", queryObj["Name"] + Environment.NewLine));
                usb.AppendText("-----------------------------------------" + Environment.NewLine);
            }
        }
        public void proc()
        {
            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_Processor");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                cpusocket.Text = string.Format("CPU: {0}", queryObj["Name"]);
                Socket.Text = string.Format("Сокет: {0}", queryObj["SocketDesignation"]);

            }
        }
        public void ramspeed()
        {
            ManagementObjectSearcher searcher =
                new ManagementObjectSearcher("root\\CIMV2",
                "SELECT * FROM Win32_PhysicalMemory");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                ramspeedl.Text = string.Format("Частота памяти: {0}", queryObj["Speed"]) + " MHz";
            }

        }
        public void temperatureCPU()
        {
            Computer c = new Computer();
            c.CPUEnabled = true;
            c.Open();
            foreach (var hardware in c.Hardware)
            {
                if (hardware.HardwareType == HardwareType.CPU)
                {
                    hardware.Update();
                    foreach (var sensors in hardware.Sensors)
                    {
                        if (sensors.SensorType == SensorType.Temperature)
                        {
                            tempcpu.Text = (sensors.Max.GetValueOrDefault().ToString() + "°C");
                            circularProgressBar3.Value = (int)sensors.Max.GetValueOrDefault();
                        }
                    }
                }
            }
        }
        public void temperatureGPU()
        {
            Computer c = new Computer();
            c.GPUEnabled = true;
            c.Open();
            foreach (var hardware in c.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia)
                {
                    hardware.Update();
                    foreach (var sensors in hardware.Sensors)
                    {
                        if (sensors.SensorType == SensorType.Temperature)
                        {
                            tempgpu.Text = (sensors.Max.GetValueOrDefault().ToString() + "°C");
                            if (sensors.Max.GetValueOrDefault() > 60)
                            {
                                circularProgressBar3.ProgressColor = Color.Red;
                            }
                        }
                    }
                }
            }
        }
        public void FanSpeedGPU()
        {
            Computer c = new Computer();
            c.GPUEnabled = true;
            c.Open();
            foreach (var hardware in c.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia)
                {
                    hardware.Update();
                    foreach (var sensors in hardware.Sensors)
                    {
                        if (sensors.SensorType == SensorType.Fan)
                        {
                            gpufanspeed.Text = (sensors.Max.GetValueOrDefault().ToString() + "RPM");
                        }
                    }
                }
            }
        }
        public void videocard()
        {
            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_DisplayConfiguration");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                video.Text = string.Format("{0}", queryObj["Description"]);
            }
        }
        public void videoRAM()
        {
            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_VideoController");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                gpuram1.Text = string.Format("Объём: {0}", Math.Truncate(Convert.ToDouble(queryObj["AdapterRAM"]) / 1024 / 1024)) + "MB";
            }
        }
        public void ports()
        {

            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_PortConnector");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                Ports.AppendText(string.Format("Порт: {0}", queryObj["InternalReferenceDesignator"]) + Environment.NewLine);
                Ports.AppendText("-----------------------------------------" + Environment.NewLine);
            }

        }
        public void motherboard()
        {
            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_BaseBoard");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                Motherboard.AppendText(string.Format("{0} {1} {2}", queryObj["Manufacturer"], queryObj["Product"], queryObj["Version"]) + Environment.NewLine);
                Motherboard.AppendText("-----------------------------------------" + Environment.NewLine);
                Motherboard.AppendText(string.Format("Серийный номер: {0}", queryObj["SerialNumber"]) + Environment.NewLine);
            }

        }
        public void network()
        {
            ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_NetworkAdapter");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                Network.AppendText(string.Format("{0} ", queryObj["Name"]) + Environment.NewLine);
                Network.AppendText(string.Format("Текущая скорость: {0} ", Math.Truncate(Convert.ToDouble(queryObj["Speed"]) / 1024 / 1024)) + "MB/s" + Environment.NewLine);
                Network.AppendText("-----------------------------------------" + Environment.NewLine);
            }

        }

        public Form1()
        {
            InitializeComponent();
            //Потоки
            backgroundWorker1.RunWorkerAsync();
            backgroundWorker2.RunWorkerAsync();
        }
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            BeginInvoke(new MyDelegate(hddinfo));

            BeginInvoke(new MyDelegate(proc));

            BeginInvoke(new MyDelegate(videocard));

            BeginInvoke(new MyDelegate(ramspeed));

            BeginInvoke(new MyDelegate(videoRAM));

            BeginInvoke(new MyDelegate(temperatureCPU));

            BeginInvoke(new MyDelegate(temperatureGPU));

            backgroundWorker1.CancelAsync();
        }

        private void backgroundWorker2_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            BeginInvoke(new MyDelegate(FanSpeedGPU));

            BeginInvoke(new MyDelegate(raminfo2));

            BeginInvoke(new MyDelegate(raminfo));

            BeginInvoke(new MyDelegate(usbinfo));

            BeginInvoke(new MyDelegate(ports));

            BeginInvoke(new MyDelegate(motherboard));

            BeginInvoke(new MyDelegate(network));

            backgroundWorker2.CancelAsync();
        }


        public void timer1_Tick(object sender, EventArgs e)
        {
            var TotalRam = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
            temper.Text = ((TotalRam / 1024 / 1024) + 1).ToString();

            //Название ОС
            osviever.Text = "ОС: " + new Microsoft.VisualBasic.Devices.ComputerInfo().OSFullName;
            time.Text = new Microsoft.VisualBasic.Devices.Clock().LocalTime.ToString();
            label5.Text = new Microsoft.VisualBasic.Devices.Computer().Name;

            //Разрешение
            MonitorSize.Text = "Разрешение: " + SystemInformation.PrimaryMonitorSize.Width.ToString() + " x " + SystemInformation.PrimaryMonitorSize.Height.ToString();

            //Загрузка ЦП(невидимая)
            progressBar1.Value = (int)(pcProcessor.NextValue());
            circularProgressBar1.Value = progressBar1.Value;
            label7.Text = progressBar1.Value.ToString() + "%";
            lblProcessor.Text = "Загрузка процессора: " + progressBar1.Value.ToString() + "%";

            lblMemoryAvailable.Text = ((int)pcMemoryAvailable.NextValue()).ToString();
            circularProgressBar2.Value = (int)Memory.NextValue();

            //Частота
            cpuclock.Text = (cpu.NextValue()).ToString() + " Mhz";

            ramproc.Text = Math.Truncate(Memory.NextValue()).ToString() + "%";
            //HDD
            HDD.Text = "Свободно " + (Hddinfo1.NextValue()).ToString("00.##") + "%";
            label8.Text = (Hddinfo2.NextValue() / 1024 / 1024).ToString("0.###") + " MB/sec";

            core.Text = "Число ядер: " + Convert.ToString(Environment.ProcessorCount);
            osnumber.Text = "Ядро: " + Convert.ToString(Environment.OSVersion);
            version.Text = "Версия: " + Convert.ToString(Environment.Version);
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            FanSpeedGPU();
            temperatureCPU();
            temperatureGPU();
        }
    }
}