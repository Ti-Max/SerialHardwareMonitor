//TODO: fix crashing when sending data to other serial ports
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using OpenHardwareMonitor.Hardware;

namespace SerialHardwareMonitor
{
    [Flags]
    public enum Stats
    {
        CPU_USAGE = 1,
        CPU_TEMP = 2,
        GPU_USAGE = 4,
        GPU_TEMP = 8,
        RAM_USAGE = 16
    }

    public partial class Form1 : Form
    {
        Computer computer = new Computer()
        {
            GPUEnabled = true,
            CPUEnabled = true,
            RAMEnabled = true
        };

        Stats selectedStats;
        private SerialPort port = new SerialPort();

        public Form1()
        {
            InitializeComponent();

            InitUI();
            computer.Open();

        }

       
        private void InitUI()
        {
            //serila port options
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                Console.WriteLine(port);

                serialPorts.Items.Add(port);
            }

            //Default baud rate
            baudRate.SelectedItem = "9600";

            // Available sensors
            foreach (var stat in Enum.GetValues(typeof(Stats)))
            {
                choosedStats.Items.Add(stat);
            }

        }

        private void timerSerial_Tick(object sender, EventArgs e)
        {
            Status();
        }

        private void Start_Click(object sender, EventArgs e)
        {
            try
            {
                //Check input
                if (serialPorts.SelectedItem == null)
                    throw new Exception("Selec serial port");

                //Check chosed stats
                selectedStats = 0;
                foreach (var stat in choosedStats.CheckedItems)
                {
                    selectedStats |= (Stats)stat;
                }
                if (selectedStats == 0)
                    throw new Exception("Choose at least one sensor value!");

                //Open serial port
                port.PortName = serialPorts.SelectedItem.ToString();
                port.BaudRate = Convert.ToInt32(baudRate.SelectedItem);
                port.Open();

                // start timer
                timerSerial.Enabled = true;
                timerSerial.Interval = (int)interval.Value;

                // Disable inputs
                serialPorts.Enabled = false;
                baudRate.Enabled = false;
                interval.Enabled = false;
                Start.Enabled = false;
                choosedStats.Enabled = false;
                Stop.Enabled = true;

                // Change icon
                
                notifyIcon1.Icon = (Icon)Properties.Resources.ResourceManager.GetObject("icon green.ico");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            // Close port
            try
            {
                //port.Write("DIS*");
                port.Close();

                //Stop timer
                timerSerial.Enabled = false;

                // Enable inputs
                serialPorts.Enabled = true;
                baudRate.Enabled = true;
                interval.Enabled = true;
                Start.Enabled = true;
                choosedStats.Enabled = true;
                Stop.Enabled = false;

                //clear raw data
                rawData.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Status()
        {
            List<string> values = new List<string>();

            if (selectedStats.HasFlag(Stats.CPU_USAGE))
            {
                int value = (int)GetSensorsValue(HardwareType.CPU, SensorType.Load, "CPU Total");
                values.Add(value.ToString());
            }

            if (selectedStats.HasFlag(Stats.CPU_TEMP))
            {
                int value = (int)GetAvgTemp(HardwareType.CPU);
                values.Add(value.ToString());
            }

            if (selectedStats.HasFlag(Stats.GPU_USAGE))
            {
                //One of them will return 0;
                int amd = (int)GetSensorsValue(HardwareType.GpuAti, SensorType.Load, "GPU Core");
                int nvidia = (int)GetSensorsValue(HardwareType.GpuNvidia, SensorType.Load, "GPU Core");

                values.Add(Math.Max(amd, nvidia).ToString());
            }

            if (selectedStats.HasFlag(Stats.GPU_TEMP))
            {
                //One of them will return 0;
                int amd = (int)GetSensorsValue(HardwareType.GpuAti, SensorType.Temperature, "GPU Core");
                int nvidia = (int)GetSensorsValue(HardwareType.GpuNvidia, SensorType.Temperature, "GPU Core");

                values.Add(Math.Max(amd, nvidia).ToString());
            }
            if (selectedStats.HasFlag(Stats.RAM_USAGE))
            {
                int value = (int)GetSensorsValue(HardwareType.RAM, SensorType.Load, "Memory");
                values.Add(value.ToString());

            }

            try
            {
                // Put ; between each value and E to the end
                string message = string.Join(";", values.ToArray()) + "E";
                byte[] data = Encoding.ASCII.GetBytes(message);
                port.Write(data, 0, data.Length);

                //display raw data
                rawData.Text = message;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        private float GetSensorsValue(HardwareType type, SensorType sensorType, string Name)
        {
            // Find hardware
            IHardware hardware = computer.Hardware.Where(device => device.HardwareType == type).FirstOrDefault();
            if (hardware == null)
                return 0.0f;

            hardware.Update();

            // Find needes sensor
            ISensor sensor = hardware.Sensors.Where(sensor1 => sensor1.SensorType == sensorType && sensor1.Name == Name).FirstOrDefault();
            if (sensor == null)
                return 0.0f;
            return sensor.Value.GetValueOrDefault();
        }

        private float GetAvgTemp(HardwareType type)
        {
            var gpus = computer.Hardware.Where(x => x.HardwareType == type).ToArray();
            if (gpus.Any())
            {
                int n = 0;
                float t = 0;
                foreach (var gpu in gpus)
                {
                    var temps = gpu.Sensors.Where(x => x.SensorType == SensorType.Temperature).ToArray();
                    if (temps.Any())
                    {
                        var temp = temps.Average(x => x.Value.Value);
                        t += temp;
                        n++;
                    }

                    foreach (var sh in gpu.SubHardware)
                    {
                        temps = sh.Sensors.Where(x => x.SensorType == SensorType.Temperature).ToArray();
                        if (temps.Any())
                        {
                            var temp = temps.Average(x => x.Value.Value);
                            t += temp;
                            n++;
                        }
                    }
                }

                if (n > 0)
                {
                    return t / (float)n;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        ~Form1()
        {
            port.Close();
            port.Dispose();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
            }
        }
    }
}
