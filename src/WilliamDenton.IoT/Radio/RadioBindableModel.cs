using System;
using System.ComponentModel;
using System.Threading.Tasks;
using WilliamDenton.IoT.Model;
using WilliamDenton.IoT.Radio.RDS;


/// <summary>
/// More information and source code is available at https://github.com/williamdenton/IoT
/// </summary>
namespace WilliamDenton.IoT.Radio
{
    public class RadioObservableModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private IRadioTuner _tuner;


        public RadioObservableModel() { }

        public RadioObservableModel(IRadioTuner tuner)
        {
            _tuner = tuner;
            ClearRDS();
        }

        private bool _isPoweredOn;
        public bool IsPoweredOn
        {
            get { return _isPoweredOn; }
            internal set
            {
                if (_isPoweredOn != value)
                {
                    _isPoweredOn = value;
                    OnPropertyChanged(nameof(IsPoweredOn));
                }
            }
        }

        private void ClearRDS()
        {
            RDSProgramIndicator = 0;
            RDSProgramName = string.Empty;
            RDSProgramType = 0;
            RDSRadioText = string.Empty;
        }

        private float _frequency;
        public float Frequency
        {
            get { return _frequency; }
            internal set
            {
                if (_frequency != value)
                {
                    _frequency = value;
                    OnPropertyChanged(nameof(Frequency));

                    //changing the frequency invalidates all RDS data
                    ClearRDS();
                }
            }
        }

        private UInt16 _RDSProgramIndicator;
        public UInt16 RDSProgramIndicator
        {
            get { return _RDSProgramIndicator; }
            internal set
            {
                if (_RDSProgramIndicator != value)
                {
                    _RDSProgramIndicator = value;
                    OnPropertyChanged(nameof(RDSProgramIndicator));
                }
            }
        }

        private byte _RDSProgramType;
        public byte RDSProgramType
        {
            get { return _RDSProgramType; }
            internal set
            {
                if (_RDSProgramType != value)
                {
                    _RDSProgramType = value;
                    OnPropertyChanged(nameof(RDSProgramType));
                    OnPropertyChanged(nameof(RDSProgramTypeString));
                }
            }
        }

        public string RDSProgramTypeString
        {
            get { return RDSProgramTypeLookup.GetProgramTypeEU(_RDSProgramType); }
        }

        private string _RDSProgramName;
        public string RDSProgramName
        {
            get { return _RDSProgramName; }
            internal set
            {
                if (_RDSProgramName != value)
                {
                    _RDSProgramName = value;
                    OnPropertyChanged(nameof(RDSProgramName));
                }
            }
        }

        private string _RDSRadioText;
        public string RDSRadioText
        {
            get { return _RDSRadioText; }
            internal set
            {
                if (_RDSRadioText != value)
                {
                    _RDSRadioText = value;
                    OnPropertyChanged(nameof(RDSRadioText));
                }
            }
        }

        private UInt16 _RSSI;
        public UInt16 RSSI
        {
            get { return _RSSI; }
            internal set
            {
                if (_RSSI != value)
                {
                    _RSSI = value;
                    OnPropertyChanged(nameof(RSSI));
                }
            }
        }

        private bool _isStereo;
        public bool isStereo
        {
            get { return _isStereo; }
            internal set
            {
                if (_isStereo != value)
                {
                    _isStereo = value;
                    OnPropertyChanged(nameof(isStereo));
                }
            }
        }

        private UInt16 _Volume;
        public UInt16 Volume
        {
            get { return _Volume; }
            internal set
            {
                if (_Volume != value)
                {
                    _Volume = value;
                    OnPropertyChanged(nameof(Volume));
                }
            }
        }

        private RelayCommand _PowerOn;
        public RelayCommand PowerOn
        {
            get
            {
                if (_PowerOn == null)
                {
                    Predicate<object> canexecute = (o) => { return !IsPoweredOn; };
                    Action<object> execute = (o) => { _tuner.PowerOn(); };
                    _PowerOn = new RelayCommand(execute, canexecute);
                }
                return _PowerOn;
            }
        }

        private RelayCommand _PowerOff;
        public RelayCommand PowerOff
        {
            get
            {
                if (_PowerOff == null)
                {
                    Predicate<object> canexecute = (o) => { return IsPoweredOn; };
                    Action<object> execute = (o) => { _tuner.PowerOff(); };
                    _PowerOff = new RelayCommand(execute, canexecute);
                }
                return _PowerOff;
            }
        }

        private RelayAsyncCommand _SeekDown;
        public RelayAsyncCommand SeekDown
        {
            get
            {
                if (_SeekDown == null)
                {
                    Predicate<object> canexecute = (o) => { return IsPoweredOn && _tuner.CanTune; };
                    Func<object, Task> execute = async (o) =>
                    {

                        await _tuner.SeekAsync(false);

                    };
                    _SeekDown = new RelayAsyncCommand(execute, canexecute);
                }
                return _SeekDown;
            }
        }

        private RelayAsyncCommand _SeekUp;
        public RelayAsyncCommand SeekUp
        {
            get
            {
                if (_SeekUp == null)
                {
                    Predicate<object> canexecute = (o) => { return IsPoweredOn && _tuner.CanTune; };
                    Func<object, Task> execute = async (o) =>
                    {
                        await _tuner.SeekAsync(true);
                    };
                    _SeekUp = new RelayAsyncCommand(execute, canexecute);
                }
                return _SeekUp;
            }
        }


        private double? _TuneTo;
        public double? TuneTo
        {
            get { return _TuneTo; }
            set
            {
                if (value > 108)
                {
                    value = null;
                }
                if (value < 87.5)
                {
                    value = null;
                }
                if (value.HasValue)
                {
                    value = Math.Round(value.Value, 1);
                }
                if (_TuneTo != value)
                {
                    _TuneTo = value;
                    OnPropertyChanged(nameof(TuneTo));
                }
            }
        }


        private RelayAsyncCommand _Tune;
        public RelayAsyncCommand Tune
        {
            get
            {
                if (_Tune == null)
                {
                    Predicate<object> canexecute = (o) => { return IsPoweredOn && _tuner.CanTune && TuneTo.HasValue; };
                    Func<object, Task> execute = async (o) =>
                   {
                       UInt16 frequency = (UInt16)(TuneTo.Value * 10);
                       await _tuner.TuneAsync(frequency);
                   };
                    _Tune = new RelayAsyncCommand(execute, canexecute);
                }
                return _Tune;
            }
        }

        private RelayCommand _VolumeUp;
        public RelayCommand VolumeUp
        {
            get
            {
                if (_VolumeUp == null)
                {
                    Predicate<object> canexecute = (o) => { return IsPoweredOn && Volume < _tuner.MaxVolume; };
                    Action<object> execute = (o) => { _tuner.SetVolume((UInt16)(Volume + 1)); };
                    _VolumeUp = new RelayCommand(execute, canexecute);
                }
                return _VolumeUp;
            }
        }

        private RelayCommand _VolumeDown;
        public RelayCommand VolumeDown
        {
            get
            {
                if (_VolumeDown == null)
                {
                    Predicate<object> canexecute = (o) => { return IsPoweredOn && Volume > 0; };
                    Action<object> execute = (o) => { _tuner.SetVolume((UInt16)(Volume - 1)); };
                    _VolumeDown = new RelayCommand(execute, canexecute);
                }
                return _VolumeDown;
            }
        }




        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }


}
