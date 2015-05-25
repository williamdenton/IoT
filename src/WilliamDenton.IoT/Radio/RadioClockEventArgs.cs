using System;

/// <summary>
/// More information and source code is available at https://github.com/williamdenton/IoT
/// </summary>
namespace WilliamDenton.IoT.Radio
{
    public class RadioClockEventArgs : EventArgs
    {
        public int MinutesPastMidnight { get; private set; }
        public RadioClockEventArgs(int minutesPastMidnight)
        {
            this.MinutesPastMidnight = minutesPastMidnight;
        }
    }
}
