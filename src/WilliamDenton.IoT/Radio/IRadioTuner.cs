using System;
using System.Threading.Tasks;


/// <summary>
/// More information and source code is available at https://github.com/williamdenton/IoT
/// </summary>
namespace WilliamDenton.IoT.Radio
{

    public interface IRadioTuner : IDisposable
    {
        Task PowerOn();
        void PowerOff();

        RadioObservableModel ObvervableRadio {get;}
        bool CanTune { get; }

        Task<bool> SeekAsync(bool up);
        Task<bool> TuneAsync(UInt16 frequency);

        UInt16 MaxVolume { get; }
        void SetVolume(UInt16 volume);

    }

}


