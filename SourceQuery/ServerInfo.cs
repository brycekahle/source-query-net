using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace SourceQuery
{
    [Serializable]
    public enum ServerType : byte
    {
        Listen = 0x6C, //'l'
        Dedicated = 0x64,// 'd'
        SourceTv = 0x70,// 'p'
    }

    [Serializable]
    public enum OperatingSystem : byte
    {
        Linux = 0x6C,
        Windows = 0x77
    }

    [Flags]
    [Serializable]
    public enum ExtraDataFlags : byte
    {
        GamePort = 0x80,
        SpectatorInfo = 0x40,
        GameTagData = 0x20,
        SteamID = 0x10,
        GameID = 0x01,
    }
}
