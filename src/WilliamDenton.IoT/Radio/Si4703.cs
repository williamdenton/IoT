using System;
using System.Threading;
using System.Threading.Tasks;
using WilliamDenton.IoT.Radio.RDS;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

/// <summary>
/// More information and source code is available at https://github.com/williamdenton/IoT
/// </summary>
namespace WilliamDenton.IoT.Radio
{
    public sealed class Si4703 : IDisposable, IRadioTuner
    {

        // General Programming Guide http://cdn.sparkfun.com/datasheets/BreakoutBoards/AN230.
        // RDS Guide http://cdn.sparkfun.com/datasheets/BreakoutBoards/AN243.pdf
        // Register Descriptions (Page 22) https://www.sparkfun.com/datasheets/BreakoutBoards/Si4702-03-C19-1.pdf

        #region I2C Addresses
        const UInt16 SI4703 = 0x10; //0b._001.0000 = I2C address of Si4703 - note that the Wire function assumes non-left-shifted I2C address, not 0b.0010.000W 

        const UInt16 SEEK_DOWN = 0; //Direction used for seeking. Default is down 
        const UInt16 SEEK_UP = 1;


        //Define the register names 
        const UInt16 DEVICEID = 0x00;
        const UInt16 CHIPID = 0x01;
        const UInt16 POWERCFG = 0x02; //power config(mute, seek, rdsmode)
        const UInt16 CHANNEL = 0x03; //tune
        const UInt16 SYSCONFIG1 = 0x04;
        const UInt16 SYSCONFIG2 = 0x05;
        const UInt16 SYSCONFIG3 = 0x06;
        const UInt16 TEST1 = 0x07;
        const UInt16 TEST2 = 0x08;
        const UInt16 BOOTCONFIG = 0x09;

        const UInt16 STATUSRSSI = 0x0A;//status rssi (stereo ind, signal strength)
        const UInt16 READCHAN = 0x0B;
        const UInt16 RDSA = 0x0C;
        const UInt16 RDSB = 0x0D;
        const UInt16 RDSC = 0x0E;
        const UInt16 RDSD = 0x0F;


        //Register 0x02 - POWERCFG 
        const UInt16 SMUTE = 15;
        const UInt16 DMUTE = 14;
        const UInt16 MONO = 13;
        const UInt16 SKMODE = 10;
        const UInt16 SEEKUP = 9;
        const UInt16 SEEK = 8;
        const UInt16 ENABLE = 1;
        const UInt16 DISABLE = 6;


        //Register 0x03 - CHANNEL 
        const UInt16 TUNE = 15;


        //Register 0x04 - SYSCONFIG1 
        const UInt16 GPIO1 = 0;
        const UInt16 GPIO2 = 2;
        const UInt16 GPIO3 = 4;
        const UInt16 RDS = 12;
        const UInt16 DE = 11;
        const UInt16 STCIEN = 14;
        const UInt16 RDSIEN = 15;


        //Register 0x05 - SYSCONFIG2 
        const UInt16 SPACE1 = 5;
        const UInt16 SPACE0 = 4;





        //Register 0x0A - STATUSRSSI 
        const UInt16 RDSR = 15;
        const UInt16 STC = 14;
        const UInt16 SFBL = 13;
        const UInt16 AFCRL = 12;
        const UInt16 RDSS = 11;
        const UInt16 STEREO = 8;

        #endregion

        // private const string I2C_CONTROLLER_NAME = "I2C5";        /* For Minnowboard Max, use I2C5 */ 
        private const string I2C_CONTROLLER_NAME = "I2C1";        /* For Raspberry Pi 2, use I2C1 */

        private const UInt16 INTERNAL_FREQUENCY_OFFSET = 875;

        private GpioPin _resetPin;
        private GpioPin _gpio2Pin;
        private I2cDevice _i2cComms;
        private RDSDecoder _rdsDecoder;
        private bool tuneOperationInProgress = false;

        private Windows.UI.Xaml.DispatcherTimer _UiUpdateTimer;

        // public event EventHandler<RadioClockEventArgs> BroadcastTimeReceived;

        private readonly SynchronizationContext _OwnerThreadSyncContext;

        public Si4703(int resetPinNo, int gpio2PinNo)
        {
            var gpio = GpioController.GetDefault();
            _resetPin = gpio.OpenPin(resetPinNo);
            _resetPin.SetDriveMode(GpioPinDriveMode.Output);

            _gpio2Pin = gpio.OpenPin(gpio2PinNo);
            _gpio2Pin.SetDriveMode(GpioPinDriveMode.Input);
            _gpio2Pin.ValueChanged += Gpio2PinRDS_ValueChanged;


            ObvervableRadio = new RadioObservableModel(this);
            ObvervableRadio.IsPoweredOn = false;

            _UiUpdateTimer = new Windows.UI.Xaml.DispatcherTimer();
            _UiUpdateTimer.Interval = TimeSpan.FromMilliseconds(100);
            _UiUpdateTimer.Tick += UiUpdateTimer_Tick;
            _UiUpdateTimer.Start();
            // we assume this ctor is called from the UI thread!

            _OwnerThreadSyncContext = SynchronizationContext.Current;
        }

        private async Task InitI2CBus()
        {
            if (_i2cComms == null)
            {
                var settings = new I2cConnectionSettings(SI4703);
                settings.BusSpeed = I2cBusSpeed.FastMode;

                string aqs = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);  /* Find the selector string for the I2C bus controller                   */
                var dis = await DeviceInformation.FindAllAsync(aqs);            /* Find the I2C bus controller device with our selector string           */
                _i2cComms = await I2cDevice.FromIdAsync(dis[0].Id, settings);    /* Create an I2cDevice with our selected bus controller and I2C settings */
            }
        }


        public RadioObservableModel ObvervableRadio { get; private set; }

        public UInt16 MaxVolume { get { return 15; } }

        public bool CanTune
        {
            get
            {
                return !tuneOperationInProgress;
            }
        }

        public async Task PowerOn()
        {
            ThrowIfDisposed();

            _resetPin.Write(GpioPinValue.Low);
            await Task.Delay(1);
            _resetPin.Write(GpioPinValue.High);
            await Task.Delay(1);

            await InitI2CBus();

            var si470x_shadow = ReadRegisters();

            var info = GetSi470XInfo(si470x_shadow);
            si470x_shadow[POWERCFG] &= 1 << ENABLE;
            unchecked
            {
                si470x_shadow[POWERCFG] &= (UInt16)~(1 << DISABLE);
            }
            si470x_shadow[TEST1] = 0x8100;
            WriteRegisters(si470x_shadow);

            await Task.Delay(500);

            si470x_shadow = ReadRegisters();

            si470x_shadow[POWERCFG] = 0x4001;
            si470x_shadow[SYSCONFIG1] |= 1 << RDS;

            si470x_shadow[SYSCONFIG2] &= 0xFFF0; //Clear volume bits
            si470x_shadow[SYSCONFIG2] |= 0x0001; //Set volume to lowest
            si470x_shadow[SYSCONFIG2] |= 1 << SPACE0;

            ObvervableRadio.Volume = 1;


            si470x_shadow[SYSCONFIG1] |= 1 << RDSIEN;//enable RDS interrupt on GPIO2
            si470x_shadow[SYSCONFIG1] |= 1 << STCIEN;//enable STC interrupt on GPIO2
            si470x_shadow[SYSCONFIG1] |= 1 << GPIO2;//enable  interrupt on GPIO2


            WriteRegisters(si470x_shadow);

            await Task.Delay(150);

            si470x_shadow = ReadRegisters();
            ObvervableRadio.Frequency = GetFrequency(si470x_shadow);

            ObvervableRadio.IsPoweredOn = true;
        }

        public void PowerOff()
        {
            ThrowIfDisposed();
            UInt16[] si470x_shadow = ReadRegisters();
            si470x_shadow[POWERCFG] &= 1 << ENABLE;
            si470x_shadow[POWERCFG] &= 1 << DISABLE;
            unchecked
            {
                //must disable RDS when powering down
                si470x_shadow[SYSCONFIG1] &= (UInt16)(~1 << RDS);
            }
            WriteRegisters(si470x_shadow);
            ObvervableRadio.IsPoweredOn = false;
            ObvervableRadio.Frequency = 0;
            ObvervableRadio.RSSI = 0;
        }

        public Si4700Info GetSi4700Info()
        {
            var si470x_shadow = ReadRegisters();
            return GetSi470XInfo(si470x_shadow);
        }

        public Task<bool> SeekAsync(bool up)
        {
            ThrowIfDisposed();
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            return SeekOrTuneAsync(0, up, cts.Token);
        }

        public Task<bool> TuneAsync(UInt16 frequency)
        {
            ThrowIfDisposed();
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(1));

            return SeekOrTuneAsync(frequency, true, cts.Token);
        }

        public void SetVolume(UInt16 volume)
        {
            ThrowIfDisposed();

            if (volume > UInt16.MaxValue / 2)
            {
                //its unsigned, so when volume goes down from 0 it wraps to max 
                volume = 0;
            }
            if (volume > MaxVolume)
            {
                volume = MaxVolume;
            }
            UInt16[] si470x_shadow = ReadRegisters();

            si470x_shadow[SYSCONFIG2] &= 0xFFF0; //clear volume bits 
            si470x_shadow[SYSCONFIG2] |= volume; //set new volume 
            WriteRegisters(si470x_shadow);

            ObvervableRadio.Volume = volume;
        }

        public void SetMono(bool mono)
        {
            ThrowIfDisposed();

            UInt16[] si470x_shadow = ReadRegisters();
            if (mono)
            {
                si470x_shadow[POWERCFG] |= (1 << MONO);
            }
            else
            {
                unchecked
                {
                    si470x_shadow[POWERCFG] |= (UInt16)~(1 << MONO);
                }
            }
            WriteRegisters(si470x_shadow);

        }




        private void UiUpdateTimer_Tick(object sender, object e)
        {
            if (ObvervableRadio.IsPoweredOn)
            {
                var si470x_shadow = ReadRegisters();
                var rssi = GetRSSI(si470x_shadow);
                var RSSI = rssi;
                var isStereo = GetStereo(si470x_shadow);

                SendOrPostCallback dlg = new SendOrPostCallback(new Action<object>((o) =>
                {
                    ObvervableRadio.isStereo = isStereo;
                    ObvervableRadio.RSSI = RSSI;
                }));

                _OwnerThreadSyncContext.Post(dlg, null);

            }
        }

        private UInt16[] ReadRegisters()
        {
            var i2cBuffer = new byte[32];
            lock (_i2cComms)
            {
                //there is a chance this may be called from two threads as we are using events
                // - gpio interrupt
                // - timer
                _i2cComms.Read(i2cBuffer);
            }

            var si470x_shadow = ConvertI2CBufferToRegisters(i2cBuffer);

            return si470x_shadow;
        }

        private void WriteRegisters(UInt16[] si470x_shadow)
        {
            var i2cBuffer = ConvertRegistersToI2CBuffer(si470x_shadow);
            lock (_i2cComms)
            {
                //there is a chance this may be called from two threads as we are using events
                // - gpio interrupt
                // - timer
                _i2cComms.Write(i2cBuffer);
            }
        }

        private void ProcessRDS(UInt16[] si470x_shadow)
        {

            bool rdsReady = GetRDSSync(si470x_shadow);

            if (_rdsDecoder != null && rdsReady)
            {
                _rdsDecoder.ProcessData(
                                          si470x_shadow[RDSA],
                                          si470x_shadow[RDSB],
                                          si470x_shadow[RDSC],
                                          si470x_shadow[RDSD]
                                          );

                var RDSProgramIndicator = _rdsDecoder.ProgramIdentifier;
                var RDSProgramName = _rdsDecoder.ProgramName;
                var RDSProgramType = _rdsDecoder.ProgramType;
                var RDSRadioText = _rdsDecoder.RadioText;
                var isStereo = GetStereo(si470x_shadow);
                var RSSI = GetRSSI(si470x_shadow);

                SendOrPostCallback dlg = new SendOrPostCallback(new Action<object>((o) =>
                {

                    ObvervableRadio.RDSProgramIndicator = RDSProgramIndicator;
                    ObvervableRadio.RDSProgramName = RDSProgramName;
                    ObvervableRadio.RDSProgramType = RDSProgramType;
                    ObvervableRadio.RDSRadioText = RDSRadioText;

                    ObvervableRadio.isStereo = isStereo;
                    ObvervableRadio.RSSI = RSSI;

                }));

                _OwnerThreadSyncContext.Post(dlg, null);
            }
        }

        private void Gpio2PinRDS_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (args.Edge == GpioPinEdge.FallingEdge)
            {
                UInt16[] si470x_shadow = ReadRegisters();

                var rdsReady = GetRDSSync(si470x_shadow);

                if (rdsReady)
                {
                    ProcessRDS(si470x_shadow);
                }

                //needs thread fixing - rds decoder is null after we get back from updating the UI
                //if (rdsDecoder.TimeUpdated)
                //{
                //    rdsDecoder.TimeUpdated = false;
                //    var rcea = new RadioClockEventArgs()
                //    {
                //        Time = new DateTimeOffset(DateTimeOffset.Now.Date).AddMinutes(rdsDecoder.Time)
                //    };
                //    if (BroadcastTimeReceived != null)
                //    {
                //        BroadcastTimeReceived.Invoke(this, rcea);
                //    }

                //    System.Diagnostics.Debug.WriteLine(rcea.Time);

                //}
            }
        }

        private void BeginTune(UInt16 frequency)
        {
            UInt16 channel = (UInt16)(frequency - INTERNAL_FREQUENCY_OFFSET);

            UInt16[] si470x_shadow = ReadRegisters();
            si470x_shadow[CHANNEL] &= 0xFE00; //set channel to 00 
            si470x_shadow[CHANNEL] |= channel; // set the new channel 
            si470x_shadow[CHANNEL] |= (1 << TUNE); // set the tune bit 
            WriteRegisters(si470x_shadow);
        }

        private void EndTune(UInt16[] si470x_shadow)
        {
            if (tuneOperationInProgress)
            {
                unchecked
                {
                    si470x_shadow[CHANNEL] &= (UInt16)~(1 << TUNE); // clear the tune bit 
                }
                WriteRegisters(si470x_shadow);
                tuneOperationInProgress = false;
            }
        }

        private void BeginSeek(bool up)
        {
            UInt16[] si470x_shadow = ReadRegisters();
            si470x_shadow[POWERCFG] |= (1 << SKMODE); // allow wrap 
            if (up)
            {
                si470x_shadow[POWERCFG] |= (1 << SEEKUP); // set seek direction 
            }
            else
            {
                unchecked
                {
                    si470x_shadow[POWERCFG] &= (UInt16)~(1 << SEEKUP); // set seek direction 
                }
            }

            si470x_shadow[POWERCFG] |= (1 << SEEK); // start seek 
            si470x_shadow[SYSCONFIG3] |= (1 << 4); // set min seek threshold 
            si470x_shadow[SYSCONFIG3] |= (1 << 1); //set min FM Imp. threshold 
            WriteRegisters(si470x_shadow);
        }

        private void EndSeek(UInt16[] si470x_shadow)
        {
            if (tuneOperationInProgress)
            {
                unchecked
                {
                    si470x_shadow[POWERCFG] &= (UInt16)~(1 << SEEK); // clear the tune bit 
                }
                WriteRegisters(si470x_shadow);
                tuneOperationInProgress = false;
            }
        }

        private async Task<bool> SeekOrTuneAsync(UInt16 frequency, bool up, CancellationToken cancellationToken)
        {
            if (tuneOperationInProgress)
            {
                throw new InvalidOperationException("Async Tuning Operation Already In Progress");
            }
            tuneOperationInProgress = true;

            var tcs = new TaskCompletionSource<UInt16[]>();

            //this lambda is called when the gpio2 pin is triggered, signalling a finished tune operation
            Windows.Foundation.TypedEventHandler<GpioPin, GpioPinValueChangedEventArgs> taskFinishedLambda = (s, a) =>
            {
                if (a.Edge == GpioPinEdge.FallingEdge)
                {
                    var si470x_shadow = ReadRegisters();
                    //this event is also used for RDS - check the STC bit is set
                    var stcBit = IsSTCSet(si470x_shadow);
                    if (!stcBit)
                    {
                        //tune or seek operation is finished
                        //signal the task to complete
                        tcs.TrySetResult(si470x_shadow);
                    }
                }
            };

            //this lambda is called when the cancelltion token times out
            cancellationToken.Register(() =>
            {
                //the timeout has expired, the tune or seek operation has failed
                tcs.TrySetCanceled();
            });

            try
            {
                //stop polling for signal strength
                //keeping the I2Cbus quiet while tuning improves performance
                _UiUpdateTimer.Stop();

                _rdsDecoder = null;

                _gpio2Pin.ValueChanged += taskFinishedLambda;

                if (frequency == 0)
                {
                    BeginSeek(up);
                }
                else
                {
                    BeginTune(frequency);
                }

                var si470x_shadow = await tcs.Task;

                bool tunedOk = (si470x_shadow[STATUSRSSI] & (1 << SFBL)) == 0;

                ObvervableRadio.Frequency = GetFrequency(si470x_shadow) / 10f;

                if (frequency == 0)
                {
                    EndSeek(si470x_shadow);
                }
                else
                {
                    EndTune(si470x_shadow);
                }

                //refresh the RDS decoder
                _rdsDecoder = new RDSDecoder();

                return tunedOk;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            finally
            {
                _gpio2Pin.ValueChanged -= taskFinishedLambda;
                _UiUpdateTimer.Start();
                if (tuneOperationInProgress)
                {
                    //we must always call EndSeek/Tune but it is more efficient 
                    //to do it above when we already have the registers here we 
                    //must re-read the registers so we can clear the seek or tune 
                    //flags. This should only occur when there is an exception.
                    //We can't use the exception handler to call this as we are only 
                    //handling TaskCancelledExceptions and it must be called 
                    //regardless of exception type
                    var si470x_shadow = ReadRegisters();
                    if (frequency == 0)
                    {
                        EndSeek(si470x_shadow);
                    }
                    else
                    {
                        EndTune(si470x_shadow);
                    }
                }
            }
        }




        #region Static Decoder Methods

        private static bool GetIsMono(UInt16[] si470x_shadow)
        {
            bool isMono = ((si470x_shadow[POWERCFG] & 1 << MONO)) == 0;
            return isMono;
        }

        private static bool GetIsRdsSync(UInt16[] si470x_shadow)
        {
            bool isRDS = ((si470x_shadow[STATUSRSSI] & 1 << RDSR)) == 0;
            return isRDS;
        }

        private static bool IsSTCSet(UInt16[] si470x_shadow)
        {
            var stcBit = si470x_shadow[STATUSRSSI] & (1 << STC);
            return stcBit == 0;
        }

        private static UInt16 GetFrequency(UInt16[] si470x_shadow)
        {
            UInt16 channel = (UInt16)(si470x_shadow[READCHAN] & 0x1FF);
            channel = (UInt16)(channel + INTERNAL_FREQUENCY_OFFSET);
            return channel;
        }

        private static Si4700Info GetSi470XInfo(UInt16[] si470x_shadow)
        {
            var pn = si470x_shadow[DEVICEID] >> 12;
            var mfgid = si470x_shadow[DEVICEID] & 0x7FF;
            var rev = si470x_shadow[CHIPID] >> 10;
            var dev = (si470x_shadow[CHIPID] >> 6) & 0x0F;
            var firmware = si470x_shadow[CHIPID] & 0x3F;

            return new Si4700Info()
            {
                DEV = (UInt16)dev,
                FIRMWARE = (UInt16)firmware,
                MFGID = (UInt16)mfgid,
                PN = (UInt16)pn,
                REV = (UInt16)rev
            };
        }

        private static bool GetRDSSync(UInt16[] si470x_shadow)
        {
            bool rdsSynced = ((si470x_shadow[STATUSRSSI] >> RDSR) & 0x01) == 1;
            return rdsSynced;
        }
        private static bool GetStereo(UInt16[] si470x_shadow)
        {
            bool stereo = (si470x_shadow[STATUSRSSI] & (1 << STEREO)) != 0;
            return stereo;

        }

        private static UInt16 GetRSSI(UInt16[] si470x_shadow)
        {
            UInt16 rssi = (UInt16)(si470x_shadow[STATUSRSSI] & 0xEF);
            return rssi;
        }

        private static UInt16 GetVolume(UInt16[] si470x_shadow)
        {
            UInt16 volume = si470x_shadow[SYSCONFIG2];
            return volume;
        }

        private static UInt16[] ConvertI2CBufferToRegisters(byte[] i2cBuffer)
        {
            var si470x_shadow = new UInt16[16];
            int bufferIdx = 0;
            for (int register = 0x0A; ; register++)
            {
                if (register == 0x10)
                {
                    register = 0; //Loop back to zero
                }
                si470x_shadow[register] = (UInt16)((i2cBuffer[bufferIdx] << 8) | i2cBuffer[bufferIdx + 1]);
                //two bytes per register
                bufferIdx += 2;
                if (register == 0x09)
                {
                    break; //We're done!
                }
            }
            return si470x_shadow;

        }

        private static byte[] ConvertRegistersToI2CBuffer(UInt16[] si470x_shadow)
        {
            byte[] i2cBuffer = new byte[32];
            int bufferIdx = 0;

            for (int register = POWERCFG; register < TEST2; register++)
            {
                //high order bits
                i2cBuffer[bufferIdx] = (byte)(si470x_shadow[register] >> 8);
                bufferIdx++;
                //low order bits
                i2cBuffer[bufferIdx] = (byte)(si470x_shadow[register] & 0xFF);
                bufferIdx++;
            }

            return i2cBuffer;
        }

        #endregion

        #region IDisposable Implementation

        // Track whether Dispose has been called.
        private bool disposed = false;

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // Take yourself off the Finalization queue 
            // to prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    PowerOff();
                    _i2cComms.Dispose();
                    // Dispose managed resources.
                    // Components.Dispose();
                }
                // Release unmanaged resources. If disposing is false, 
                // only the following code is executed.
                // CloseHandle(handle);
                //handle = IntPtr.Zero;
                // Note that this is not thread safe.
                // Another thread could start disposing the object
                // after the managed resources are disposed,
                // but before the disposed flag is set to true.
                // If thread safety is necessary, it must be
                // implemented by the client.

            }
            disposed = true;
        }

        // Use C# destructor syntax for finalization code.
        // This destructor will run only if the Dispose method 
        // does not get called.
        // It gives your base class the opportunity to finalize.
        // Do not provide destructors in types derived from this class.
        ~Si4703()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            Dispose(false);
        }


        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(Si4703));
            }
        }
        #endregion


    }


}
