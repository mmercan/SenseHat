using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenseHat
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Windows.Devices.Gpio;

   public class UltraSonic
    {
        GpioController gpio = GpioController.GetDefault();

        GpioPin TriggerPin;
        GpioPin EchoPin;

        public UltraSonic(int TriggerPin, int EchoPin)
        {
            this.TriggerPin = gpio.OpenPin(TriggerPin);
            this.EchoPin = gpio.OpenPin(EchoPin);

            this.TriggerPin.SetDriveMode(GpioPinDriveMode.Output);
            this.EchoPin.SetDriveMode(GpioPinDriveMode.Input);

            this.TriggerPin.Write(GpioPinValue.Low);
        }

        public double GetDistance()
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            mre.WaitOne(500);
            Stopwatch pulseLength = new Stopwatch();

            //Send pulse
            this.TriggerPin.Write(GpioPinValue.High);
            mre.WaitOne(TimeSpan.FromMilliseconds(0.01));
            this.TriggerPin.Write(GpioPinValue.Low);
            DateTime started = DateTime.Now;
            //Recieve pusle
            while (this.EchoPin.Read() == GpioPinValue.Low)
            {
                var timespan = DateTime.Now - started;
                if (timespan.TotalMilliseconds > 500.0)
                {
                    return Double.NaN;
                }
            }
            pulseLength.Start();


            while (this.EchoPin.Read() == GpioPinValue.High)
            {
            }
            pulseLength.Stop();

            //Calculating distance
            TimeSpan timeBetween = pulseLength.Elapsed;
            Debug.WriteLine(timeBetween.ToString());
            double distance = timeBetween.TotalSeconds * 17000;

            return distance;
        }

    }
}
