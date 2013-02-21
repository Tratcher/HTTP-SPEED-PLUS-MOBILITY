﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol.Framing
{
    public class SettingsFrame : Frame
    {
        // The number of bytes in the frame.
        private const int InitialFrameSize = 12;

        // Incoming
        public SettingsFrame(Frame preable)
            : base(new byte[Constants.FramePreambleSize + preable.FrameLength])
        {
            System.Buffer.BlockCopy(preable.Buffer, 0, Buffer, 0, Constants.FramePreambleSize);
        }

        // Outgoing
        public SettingsFrame(int entryCount, byte[] settings)
            : base(new byte[InitialFrameSize + settings.Length])
        {
            IsControl = true;
            Version = Constants.CurrentProtocolVersion;
            FrameType = ControlFrameType.Settings;
            FrameLength = settings.Length + InitialFrameSize - Constants.FramePreambleSize;
            EntryCount = entryCount;
            System.Buffer.BlockCopy(settings, 0, Buffer, Constants.FramePreambleSize, settings.Length);
        }

        // 32 bits
        public int EntryCount
        {
            get
            {
                return FrameHelpers.Get32BitsAt(Buffer, 8);
            }
            set
            {
                FrameHelpers.Set32BitsAt(Buffer, 8, value);
            }
        }
    }
}