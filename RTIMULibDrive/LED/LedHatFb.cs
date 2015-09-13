using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;

namespace SenseHat
{
    public class LedHatFb : IDisposable
    {
        public const byte ADDRESS = 0x46;

        #region IDisposable Support
        public void Dispose()
        {
            if (_LedHatFbDevice != null)
            {
                _LedHatFbDevice.Dispose();
                _LedHatFbDevice = null;
            }
        }
        #endregion

        private I2cDevice _LedHatFbDevice = null;

        private byte[] _gamma = {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01,
          0x02, 0x02, 0x03, 0x03, 0x04, 0x05, 0x06, 0x07,
          0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0E, 0x0F, 0x11,
          0x12, 0x14, 0x15, 0x17, 0x19, 0x1B, 0x1D, 0x1F};

        private byte[] _inverse_gamma = {
            0x00, 0x06, 0x08, 0x0A, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x14, 0x15, 0x16,
            0x16, 0x17, 0x18, 0x18, 0x19, 0x1A, 0x1A, 0x1B,
            0x1B, 0x1C, 0x1C, 0x1D, 0x1D, 0x1E, 0x1E, 0x1F};

        public async void InitHardware()
        {
            try
            {
                string aqs = I2cDevice.GetDeviceSelector();
                DeviceInformationCollection collection = await DeviceInformation.FindAllAsync(aqs);

                I2cConnectionSettings settings = new I2cConnectionSettings(ADDRESS);
                settings.BusSpeed = I2cBusSpeed.StandardMode; // 100kHz clock
                settings.SharingMode = I2cSharingMode.Exclusive;
                var _LedHatFbDeviceTask = I2cDevice.FromIdAsync(collection[0].Id, settings).AsTask();
                Task.WaitAll(_LedHatFbDeviceTask);
                _LedHatFbDevice = _LedHatFbDeviceTask.Result;
            }
            catch (Exception ex)
            {
                string error = ex.ToString();
            }
        }

        public void MapToHat(byte[] output, int outputOffset, byte[] input, int inputOffset)
        {
            for (int i = 0; i < 8; i++)
            {
                int co = inputOffset + i * 4;
                byte blue = input[co];
                byte green = input[co + 1];
                byte red = input[co + 2];
                byte alpha = input[co + 3];

                output[outputOffset + i] = (byte)(red);
                output[outputOffset + i + 8] = (byte)(green);
                output[outputOffset + i + 16] = (byte)(blue);
            }
        }

        #region low level

        private byte ReadByte(byte regAddr)
        {
            byte[] buffer = new byte[1];
            buffer[0] = regAddr;
            byte[] value = new byte[1];
            _LedHatFbDevice.WriteRead(buffer, value);
            return value[0];
        }

        private byte[] ReadBytes(byte regAddr, int length)
        {
            byte[] values = new byte[length];
            byte[] buffer = new byte[1];
            buffer[0] = regAddr;
            _LedHatFbDevice.WriteRead(buffer, values);
            return values;
        }

        void WriteByte(byte regAddr, byte data)
        {
            byte[] buffer = new byte[2];
            buffer[0] = regAddr;
            buffer[1] = data;
            _LedHatFbDevice.Write(buffer);
        }

        void WriteBytes(byte regAddr, byte[] values)
        {
            byte[] buffer = new byte[1 + values.Length];
            buffer[0] = regAddr;
            Array.Copy(values, 0, buffer, 1, values.Length);
            _LedHatFbDevice.Write(buffer);
        }

        #endregion


        public async void LoadBitmap(string name)
        {
            StorageFile srcfile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(name));
            using (IRandomAccessStream fileStream = await srcfile.OpenAsync(Windows.Storage.FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);
                BitmapTransform transform = new BitmapTransform()
                {
                    ScaledWidth = 8,
                    ScaledHeight = 8
                };
                PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage
                );

                byte[] sourcePixels = pixelData.DetachPixelData();
                byte[] hatPixels = new byte[192];

                for (int i = 0; i < (sourcePixels.Length / 4) / 8; i++)
                {
                   MapToHat(hatPixels, i * 8 * 3, sourcePixels, i * 8 * 4);
                }
                WriteLEDMatrix(hatPixels);
            }
        }

        public void WriteLED(int x, int y,int r,int g, int b)
        {
            var loc = x + (y * 24);

            byte[] bytesR = new byte[1];
            bytesR[0] = Convert.ToByte(r);

            byte[] bytesG = new byte[1];
            bytesG[0] = Convert.ToByte(g);

            byte[] bytesB = new byte[1];
            bytesB[0] = Convert.ToByte(b);
           
            var locg = loc + 8;
            var locb = loc + 16;
            WriteLEDs(loc, bytesR);
            WriteLEDs(locg, bytesG);
            WriteLEDs(locb, bytesB);
        }
        public void WriteLEDs(int address, byte[] buffer)
        {
            if (buffer.Length + address > 192)
            {
                throw new ArgumentException("Address outside range (address + buffer length must be <= 192", "buffer");
            }
            if (address < 0)
            {
                throw new ArgumentException("Address can't be less than zero", "address");
            }
            byte[] b = new byte[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                b[i] = _gamma[buffer[i] >> 3];
            }
            WriteBytes((byte)address, b);
        }

        public void WriteLEDMatrix(byte[] buffer)
        {
            WriteLEDs(0, buffer);
        }

        public byte[] ReadLEDs(int address, int size)
        {
            if (size + address > 192)
            {
                throw new ArgumentException("Address outside range (address + size length must be <= 192", "size");
            }
            if (address < 0)
            {
                throw new ArgumentException("Address can't be less than zero", "address");
            }
            byte[] b = ReadBytes((byte)address, size);
            byte[] buffer = new byte[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                buffer[i] = (byte)(_inverse_gamma[b[i] & 0x1F] << 3);
            }
            return buffer;
        }

        public byte[] ReadLEDMatrix()
        {
            return ReadLEDs(0, 192);
        }

        public byte ReadWai()
        {
            return ReadByte(0xf0);
        }

        public byte ReadVersion()
        {
            return ReadByte(0xf1);
        }

        public byte ReadKeys()
        {
            return ReadByte(0xf2);
        }

        public byte ReadEEWp()
        {
            return ReadByte(0xf4);
        }

        // no idea what this does, maybe hold off using it
        public void WriteEEEp(byte value)
        {
            WriteByte(0xf4, value);
        }

        // no idea what this does, maybe hold off using it
        public void WriteAddress(byte value)
        {
            WriteByte(0xff, value);
        }


        public void WriteLetter(char Letter)
        {
            switch (Letter)
            {

                case '0':
                    LoadBitmap("ms-appx:///assets/U0.png");
                    break;
                case '1':
                    LoadBitmap("ms-appx:///assets/U1.png");
                    break;
                case '2':
                    LoadBitmap("ms-appx:///assets/U2.png");
                    break;
                case '3':
                    LoadBitmap("ms-appx:///assets/U3.png");
                    break;
                case '4':
                    LoadBitmap("ms-appx:///assets/U4.png");
                    break;
                case '5':
                    LoadBitmap("ms-appx:///assets/U5.png");
                    break;
                case '6':
                    LoadBitmap("ms-appx:///assets/U6.png");
                    break;
                case '7':
                    LoadBitmap("ms-appx:///assets/U7.png");
                    break;
                case '8':
                    LoadBitmap("ms-appx:///assets/U8.png");
                    break;
                case '9':
                    LoadBitmap("ms-appx:///assets/U9.png");
                    break;

                case 'A':
                    LoadBitmap("ms-appx:///assets/UA.png");
                    break;
                case 'B':
                    LoadBitmap("ms-appx:///assets/UB.png");
                    break;
                case 'C':
                    LoadBitmap("ms-appx:///assets/UC.png");
                    break;
                case 'D':
                    LoadBitmap("ms-appx:///assets/UD.png");
                    break;
                case 'E':
                    LoadBitmap("ms-appx:///assets/UE.png");
                    break;
                case 'F':
                    LoadBitmap("ms-appx:///assets/UF.png");
                    break;
                case 'G':
                    LoadBitmap("ms-appx:///assets/UG.png");
                    break;
                case 'H':
                    LoadBitmap("ms-appx:///assets/UH.png");
                    break;
                case 'I':
                    LoadBitmap("ms-appx:///assets/UI.png");
                    break;
                case 'J':
                    LoadBitmap("ms-appx:///assets/UJ.png");
                    break;
                case 'K':
                    LoadBitmap("ms-appx:///assets/UK.png");
                    break;
                case 'L':
                    LoadBitmap("ms-appx:///assets/UL.png");
                    break;
                case 'M':
                    LoadBitmap("ms-appx:///assets/UM.png");
                    break;
                case 'N':
                    LoadBitmap("ms-appx:///assets/UN.png");
                    break;
                case 'O':
                    LoadBitmap("ms-appx:///assets/UO.png");
                    break;
                case 'P':
                    LoadBitmap("ms-appx:///assets/UP.png");
                    break;
                case 'Q':
                    LoadBitmap("ms-appx:///assets/UQ.png");
                    break;
                case 'R':
                    LoadBitmap("ms-appx:///assets/UR.png");
                    break;
                case 'S':
                    LoadBitmap("ms-appx:///assets/US.png");
                    break;
                case 'T':
                    LoadBitmap("ms-appx:///assets/UT.png");
                    break;
                case 'U':
                    LoadBitmap("ms-appx:///assets/UU.png");
                    break;
                case 'V':
                    LoadBitmap("ms-appx:///assets/UV.png");
                    break;
                case 'W':
                    LoadBitmap("ms-appx:///assets/UW.png");
                    break;
                case 'X':
                    LoadBitmap("ms-appx:///assets/UX.png");
                    break;
                case 'Y':
                    LoadBitmap("ms-appx:///assets/UY.png");
                    break;
                case 'Z':
                    LoadBitmap("ms-appx:///assets/UZ.png");
                    break;
               
                default:
                    break;
            }


            //WriteLED(0, 0, r, g, b);
          //  LoadBitmap("ms-appx:///assets/UA.png");
        }
    }
}