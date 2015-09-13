////////////////////////////////////////////////////////////////////////////
//
//  This file is part of RTIMULibCS
//
//  Copyright (c) 2015, richards-tech, LLC
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy of 
//  this software and associated documentation files (the "Software"), to deal in 
//  the Software without restriction, including without limitation the rights to use, 
//  copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the 
//  Software, and to permit persons to whom the Software is furnished to do so, 
//  subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all 
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
//  INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
//  PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
//  HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
//  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
//  SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Windows.UI.Xaml.Controls;
using System.Threading;
using System;
using RTIMULibCS;
using Windows.Devices.I2c;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SenseHat
{
    public sealed partial class MainPage : Page
    {

        private LedHatFb _ledHat = new LedHatFb();
        private RTIMUThread thread;
        private Timer periodicTimer;

        public  MainPage()
        {
            this.InitializeComponent();

            thread = new RTIMUThread();
             periodicTimer = new Timer(this.TimerCallback, null, 0, 500);
          

            CallmeBaby();
            this.Loaded += MainPage_Loaded;

         }

        private void MainPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {

            _ledHat.InitHardware();
           // testLeds();
        }

        double mTemperature_m;
        double mTemperature_c;
        private readonly Task Task;

        public void initTemp(I2cDevice device)
        {
            byte[] twoByte = new byte[2];
            byte[] oneByte = new byte[1];

            if (!RTI2C.Read(device, 0x2a + 0x80, twoByte))
            {
                var ErrorMessage = "Failed to read HTS221 temperature";
                return;
            }

            if (!RTI2C.Read(device, 0x33 + 0x80, oneByte))
            {
                var ErrorMessage = "Failed to read HTS221 T1_C_8";
                return;
            }
            var temp0 = oneByte[0];

            if (!RTI2C.Read(device, 0x32 + 0x80, oneByte))
            {
                var ErrorMessage = "Failed to read HTS221 T0_C_8";
                return;
            }
            byte temp1 = oneByte[0];

            var T1_C_8 = (UInt16)(((UInt16)(temp1 & 0xC) << 6) | (UInt16)temp0);
            var T1 = (double)T1_C_8 / 8.0;

            var T0_C_8 = (UInt16)((((UInt16)temp1 & 0x3) << 8) | (UInt16)temp0);
            var T0 = (double)T0_C_8 / 8.0;


            if (!RTI2C.Read(device, 0x3e + 0x80, twoByte))
            {
                var ErrorMessage = "Failed to read HTS221 T1_OUT";
                return;
            }
            var T1_OUT = (Int16)((((UInt16)twoByte[1]) << 8) | (UInt16)twoByte[0]);


            if (!RTI2C.Read(device, 0x3c + 0x80, twoByte))
            {
                var ErrorMessage = "Failed to read HTS221 T0_OUT";
                return;
            }
            var T0_OUT = (Int16)((((UInt16)twoByte[1]) << 8) | (UInt16)twoByte[0]);

            mTemperature_m = (T1 - T0) / (T1_OUT - T0_OUT);
            mTemperature_c = T0 - (mTemperature_m * T0_OUT);
        }

        public async void CallmeBaby()
        {
            string aqsFilter = I2cDevice.GetDeviceSelector("I2C1");

            DeviceInformationCollection collection = await DeviceInformation.FindAllAsync(aqsFilter);
            if (collection.Count == 0)
            {
                return;
            }
            I2cConnectionSettings settings = new I2cConnectionSettings(0x5f);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            I2cDevice device = await I2cDevice.FromIdAsync(collection[0].Id, settings);


            if (!RTI2C.Write(device, 0x20, 0x87))
            {
               var ErrorMessage = "Failed to set HTS221 CTRL_REG_1";
                return;
            }

            if (!RTI2C.Write(device, 0x10, 0x1b))
            {
              var  ErrorMessage = "Failed to set HTS221 AV_CONF";
                return;
            }

            initTemp(device);
            byte[] twoByte = new byte[2];

            if (!RTI2C.Read(device, 0x2a + 0x80, twoByte))
            {
               var ErrorMessage = "Failed to read HTS221 temperature";
                return;
            }

           double mTemperature = (Int16)((((UInt16)twoByte[1]) << 8) | (UInt16)twoByte[0]);
            mTemperature = mTemperature * mTemperature_m + mTemperature_c;
            var mTemperatureValid = true;


            //double mTemperature = (Int16)((((UInt16)twoByte[1]) << 8) | (UInt16)twoByte[0]);
            // mTemperature = mTemperature * mTemperature_m + mTemperature_c;
            //var mTemperatureValid = true;
            //var q = mTemperature;
        }


        private void TimerCallback(object state)
        {
            RTIMUData data = thread.GetIMUData;

            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                TextGyro.Text = string.Format("Gyro (radians/sec):: x: {0:F4}, y: {1:F4}, z: {2:F4}",
                                data.gyro.X, data.gyro.Y, data.gyro.Z);
                TextAccel.Text = string.Format("Accel (g):: x: {0:F4}, y: {1:F4}, z: {2:F4}",
                                data.accel.X, data.accel.Y, data.accel.Z);
                TextMag.Text = string.Format("Mag (uT):: x: {0:F4}, y: {1:F4}, z: {2:F4}",
                                data.mag.X, data.mag.Y, data.mag.Z);

                TextPose.Text = RTMath.DisplayDegrees("Pose:", data.fusionPose);
                TextQPose.Text = RTMath.Display("QPose:", data.fusionQPose);

                BiasTextStatus.Text = string.Format("Gyro bias:: {0}, Mag cal: {1}",
                    thread.GyroBiasValid ? "valid" : "invalid", thread.MagCalValid ? "valid" : "invalid");

                TextPressure.Text = string.Format("Pressure (hPa):: {0:F4}", data.pressure);
                TextHumidity.Text = string.Format("Humidity (%RH):: {0:F4}", data.humidity);
                TextTemperature.Text = string.Format("Temperature (degC):: {0:F4}", data.temperature);

                if (thread.IMUInitComplete)
                    TextRate.Text = string.Format("Rate: {0} samples per second", thread.SampleRate);

                IMUTextStatus.Text = "IMU status:: " + thread.IMUErrorMessage;
                PressureTextStatus.Text = "Pressure status:: " + thread.PressureErrorMessage;
                HumidityTextStatus.Text = "Humidity status:: " + thread.HumidityErrorMessage;

            });
        }


        private async void testLeds()
        {
           // _ledHat.InitHardware();
            //  await  Task.Delay(100);

            //_ledHat.WriteLED(7, 7, 0, 255, 255);
            _ledHat.WriteLetter('9');
            await Task.Delay(1000);
            _ledHat.WriteLetter('8');
            await Task.Delay(1000);
            _ledHat.WriteLetter('7');
            await Task.Delay(1000);
            _ledHat.WriteLetter('6');
            await Task.Delay(1000);
            _ledHat.WriteLetter('5');
            await Task.Delay(1000);
            _ledHat.WriteLetter('4');
            await Task.Delay(1000);
            _ledHat.WriteLetter('3');
            await Task.Delay(1000);
            _ledHat.WriteLetter('2');
            await Task.Delay(1000);
            _ledHat.WriteLetter('1');
            await Task.Delay(1000);
            _ledHat.WriteLetter('0');
            await Task.Delay(1000);

            //Alphabet
            _ledHat.WriteLetter('A');
            await Task.Delay(1000);
            _ledHat.WriteLetter('B');
            await Task.Delay(1000);
            _ledHat.WriteLetter('C');
            await Task.Delay(1000);
            _ledHat.WriteLetter('D');
            await Task.Delay(1000);
            _ledHat.WriteLetter('E');
            await Task.Delay(1000);
            _ledHat.WriteLetter('F');
            await Task.Delay(1000);
            _ledHat.WriteLetter('G');
            await Task.Delay(1000);
            _ledHat.WriteLetter('H');
            await Task.Delay(1000);
            _ledHat.WriteLetter('I');
            await Task.Delay(1000);
            _ledHat.WriteLetter('J');
            await Task.Delay(1000);
            _ledHat.WriteLetter('K');
            await Task.Delay(1000);
            _ledHat.WriteLetter('L');
            await Task.Delay(1000);
            _ledHat.WriteLetter('M');
            await Task.Delay(1000);
            _ledHat.WriteLetter('N');
            await Task.Delay(1000);
            _ledHat.WriteLetter('O');
            await Task.Delay(1000);
            _ledHat.WriteLetter('P');
            await Task.Delay(1000);
            _ledHat.WriteLetter('Q');
            await Task.Delay(1000);
            _ledHat.WriteLetter('R');
            await Task.Delay(1000);
            _ledHat.WriteLetter('S');
            await Task.Delay(1000);
            _ledHat.WriteLetter('T');
            await Task.Delay(1000);
            _ledHat.WriteLetter('U');
            await Task.Delay(1000);
            _ledHat.WriteLetter('V');
            await Task.Delay(1000);
            _ledHat.WriteLetter('W');
            await Task.Delay(1000);
            _ledHat.WriteLetter('X');
            await Task.Delay(1000);
            _ledHat.WriteLetter('Y');
            await Task.Delay(1000);
            _ledHat.WriteLetter('Z');
            await Task.Delay(1000);


            _ledHat.LoadBitmap("ms-appx:///assets/piq.png");
          
        }

        private void btn_init_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            testLeds();
        }

        private void btn_flip_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {

        }
        
        private void btn_dim_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {

        }

        int charnumber = 1;
        private void btn_change_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (charnumber > 9)
            {
                charnumber = 1;
            }
            _ledHat.LoadBitmap("ms-appx:///assets/char"+charnumber.ToString()+".png");
            charnumber++;
        }
    }
}
