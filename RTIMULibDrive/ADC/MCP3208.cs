using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

namespace SenseHat.ADC
{
    public class MCP3208
    {
        public readonly byte[] Port0 = new byte[3] { 0x06, 0x00, 0x00 };
        public readonly byte[] Port1 = new byte[3] { 0x06, 0x64, 0x00 };
        public readonly byte[] Port2 = new byte[3] { 0x06, 128, 0x00 };
        public readonly byte[] Port3 = new byte[3] { 0x06, 192, 0x00 };
        public readonly byte[] Port4 = new byte[3] { 0x07, 0x00, 0x00 };
        public readonly byte[] Port5 = new byte[3] { 0x07, 0x64, 0x00 };
        public readonly byte[] Port6 = new byte[3] { 0x07, 128, 0x00 };
        public readonly byte[] Port7 = new byte[3] { 0x07, 192, 0x00 };

        public SpiDevice SpiDisplay { get; private set; }

        public enum Port
        {
            Port0, Port1, Port2, Port3, Port4, Port5, Port6, Port7
        }

        public async void InitSPI(int selectline = 0)
        {
            try
            {
                var settings = new SpiConnectionSettings(selectline);
                settings.ClockFrequency = 500000;
                settings.Mode = SpiMode.Mode0;
                string deviceSelector = SpiDevice.GetDeviceSelector("SPI0");
                var deviceInfo = await DeviceInformation.FindAllAsync(deviceSelector);
                SpiDisplay = await SpiDevice.FromIdAsync(deviceInfo[0].Id, settings);
            }
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization Failed", ex);
            }
        }


        public int ReadPort(Port port = 0)
        {
            byte[] writebuffer = null;
            byte[] readBuffer = new byte[3];
            switch (port)
            {
                case Port.Port0:
                    writebuffer = Port0;
                    break;
                case Port.Port1:
                    writebuffer = Port1;
                    break;
                case Port.Port2:
                    writebuffer = Port2;
                    break;
                case Port.Port3:
                    writebuffer = Port3;
                    break;
                case Port.Port4:
                    writebuffer = Port4;
                    break;
                case Port.Port5:
                    writebuffer = Port5;
                    break;
                case Port.Port6:
                    writebuffer = Port6;
                    break;
                case Port.Port7:
                    writebuffer = Port7;
                    break;
                default:
                    break;
            }


            SpiDisplay.TransferFullDuplex(writebuffer, readBuffer);

            int result = readBuffer[1] & 0x0F;
            result <<= 8;
            result += readBuffer[2];
            return result;
        }
    }
}
