using System;

/// <summary>
/// More information and source code is available at https://github.com/williamdenton/IoT
/// </summary>
namespace WilliamDenton.IoT.Radio.RDS
{
    public class RDSDecoder
    {
        // Group Description
        // 0A Basic tuning and switching information only 
        // 0B Basic tuning and switching information only 
        // 1A Program Item Number and slow labeling codes only
        // 1B Program Item Number 
        // 2A Radiotext only 
        // 2B Radiotext only 
        // 3A Applications Identification for ODA only 
        // 3B Open Data Applications 
        // 4A Clock-time and date only 
        // 4B Open Data Applications 
        // 5A Transparent Data Channels(32 channels) or ODA 
        // 5B Transparent Data Channels(32 channels) or ODA 
        // 6A In House applications or ODA 
        // 6B In House applications or ODA 
        // 7A Radio Paging or ODA 
        // 7B Open Data Applications 
        // 8A Traffic Message Channel or ODA 
        // 8B Open Data Applications 
        // 9A Emergency Warning System or ODA 
        // 9B Open Data Applications 
        // AA Program Type Name 
        // AB Open Data Applications 
        // BA Open Data Applications 
        // BB Open Data Applications 
        // CA Open Data Applications 
        // CB Open Data Applications 
        // DA Enhanced Radio Paging or ODA 
        // DB Open Data Applications 
        // EA Enhanced Other Networks information only 
        // EB Enhanced Other Networks information only 
        // FA Undefined2 
        // FB Fast switching information only



        public string RadioText { get; private set; }
        public string ProgramName { get; private set; }
        public UInt16 ProgramIdentifier { get; private set; }
        public byte ProgramType { get; private set; }
        public int Time { get; private set; }
        public UInt16 LastEvent { get; private set; }

        public bool TextUpdated { get; set; }
        public bool TimeUpdated { get; set; }


        private char[][] programServiceNameBuffer;
        private char[] radioTextBuffer;
        private byte previousRadioTextABFlag;

        private const char NULL_CHAR = (char)0x00;

        public RDSDecoder()
        {
            programServiceNameBuffer = new char[8][];
            for (int i = 0; i < programServiceNameBuffer.Length; i++)
            {
                programServiceNameBuffer[i] = new char[2];
            }

            radioTextBuffer = new char[64];
            ProgramName = string.Empty;
            RadioText = string.Empty;
        }


        private void ProcessProgramName(UInt16 block1, UInt16 block2, UInt16 block3, UInt16 block4)
        {
            // The data received is part of the Service Station Name 
            byte idx = (byte)(2 * (block2 & 0x0003));


            // new data is 2 chars from block 4
            char c1 = (char)(block4 >> 8);
            char c2 = (char)(block4 & 0x00FF);

            if (c1 == programServiceNameBuffer[idx][0] && c2 == programServiceNameBuffer[idx + 1][0])
            {
                //matched previous, promote into validated slots
                programServiceNameBuffer[idx][1] = c1;
                programServiceNameBuffer[idx + 1][1] = c2;
            }
            else
            {
                //did not match previous
                //nuke the validated slots
                programServiceNameBuffer[idx][1] = NULL_CHAR;
                programServiceNameBuffer[idx + 1][1] = NULL_CHAR;
            }

            //write the current into the first chance slots
            programServiceNameBuffer[idx][0] = c1;
            programServiceNameBuffer[idx + 1][0] = c2;

            bool readyToPublish = true;
            char[] bufferToPublish = new char[8];
            for (int i = 0; i < programServiceNameBuffer.Length; i++)
            {
                bufferToPublish[i] = programServiceNameBuffer[i][1];
                if (bufferToPublish[i] == NULL_CHAR)
                {
                    readyToPublish = false;
                    break;
                }
            }


            if (readyToPublish)
            {
                var dedupBuffer = new string(bufferToPublish);
                if (!dedupBuffer.Equals(ProgramName))
                {
                    ProgramName = dedupBuffer;
                    TextUpdated = true;
                }
            }

        }

        private void ProcessRadioText(UInt16 block1, UInt16 block2, UInt16 block3, UInt16 block4)
        {
            byte thisRadioTextABFlag = (byte)(block2 & 0x0010);
            byte idx = (byte)(4 * (block2 & 0x000F));

            if (thisRadioTextABFlag != previousRadioTextABFlag)
            {
                //message has changed, nuke the buffer
                for (int i = 0; i < radioTextBuffer.Length; i++)
                {
                    radioTextBuffer[i] = NULL_CHAR;
                }
                previousRadioTextABFlag = thisRadioTextABFlag;
            }

            // 2 chars from block 3
            radioTextBuffer[idx] = (char)(block3 >> 8);
            idx++;
            radioTextBuffer[idx] = (char)(block3 & 0x00FF);
            idx++;

            // 2 chars from block 4
            radioTextBuffer[idx] = (char)(block4 >> 8);
            idx++;
            radioTextBuffer[idx] = (char)(block4 & 0x00FF);
            idx++;

            bool messageComplete = true;
            int idxEndOfMessage = 0;
            for (int i = 0; i < radioTextBuffer.Length; i++)
            {
                //keep a reference to where the string ends so we can copy it to a string later
                idxEndOfMessage = i;

                //an RT transmission may be terminated with an \r char or be spaced to 32 bytes
                if (radioTextBuffer[i] == '\r')
                {
                    break;
                }

                if (radioTextBuffer[i] == NULL_CHAR)
                {
                    if (i != 32)
                    {
                        //we consider a null in the 33rd slot ok
                        //previous 32 slots were non null
                        messageComplete = false;
                    }
                    break;
                }
            }

            if (messageComplete)
            {
                var dedupBuffer = new string(radioTextBuffer, 0, idxEndOfMessage);
                if (!dedupBuffer.Equals(RadioText))
                {
                    RadioText = dedupBuffer;
                    TextUpdated = true;
                }
            }



        }

        private void ProcessClock(UInt16 block1, UInt16 block2, UInt16 block3, UInt16 block4)
        {
            // Clock time and date
            byte off = (byte)(block4 & 0x3F); // 6 bits
            UInt16 mins = (UInt16)((block4 >> 6) & 0x3F); // 6 bits
            UInt16 hours = (UInt16)(((block3 & 0x0001) << 4) | ((block4 >> 12) & 0x0F));
            mins += (UInt16)(60 * hours);

            // adjust offset
            if ((off & 0x20) > 0)
            {
                mins -= (UInt16)(30 * (off & 0x1F));
            }
            else
            {
                mins += (UInt16)(30 * (off & 0x1F));
            }

            Time = mins;
            TimeUpdated = true;
        }
        public void ProcessData(UInt16 block1, UInt16 block2, UInt16 block3, UInt16 block4)
        {
            var rdsGroupType = 0x0A | ((block2 & 0xF000) >> 8) | ((block2 & 0x0800) >> 11);

            if (ProgramIdentifier != block1)
            {
                ProgramIdentifier = block1;
            }
            var pty2 = (byte)((block2 >> 6) & 0x1F);
            //var pty2 = (byte)((block2 & 0x0400) >> 8);

            ProgramType = pty2;


            LastEvent = (UInt16)rdsGroupType;

            switch (rdsGroupType)
            {
                case 0x0A:
                case 0x0B:
                    ProcessProgramName(block1, block2, block3, block4);
                    break;
                case 0x2A:
                    ProcessRadioText(block1, block2, block3, block4);
                    break;
                case 0x4a:
                    ProcessClock(block1, block2, block3, block4);
                    break;
            }
        }
    }
}
