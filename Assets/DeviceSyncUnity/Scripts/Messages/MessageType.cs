﻿using UnityEngine.Networking;

namespace DeviceSyncUnity.Messages
{
    public class MessageType
    {
        public const short Touches = MsgType.Highest + 1;
        public const short Acceleration = MsgType.Highest + 2;
    }
}
