﻿using DevicesSyncUnity.Examples.Messages;
using DevicesSyncUnity.Messages;
using DevicesSyncUnity.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace DevicesSyncUnity.Examples
{
    /// <summary>
    /// Synchronize <see cref="Lean.Touch.LeanTouch"/> information and events between devices with
    /// <see cref="LeanTouchInfoMessage"/> and <see cref="LeanTouchMessage"/>.
    /// </summary>
    public class LeanTouchSync : DevicesSyncInterval
    {
        // Properties

        /// <summary>
        /// Gets LeanTouch static information from currently connected devices.
        /// </summary>
        public Dictionary<int, LeanTouchInfoMessage> LeanTouchesInfo { get; protected set; }

        /// <summary>
        /// Gets latest LeanTouch information from currently connected devices.
        /// </summary>
        public Dictionary<int, LeanTouchMessage> LeanTouches { get; protected set; }

        // Events

        /// <summary>
        /// Called on server and on device client when a <see cref="LeanTouchInfoMessage"/> is received.
        /// </summary>
        public event Action<LeanTouchInfoMessage> LeanTouchInfoReceived = delegate { };

        /// <summary>
        /// Called on server and on device client when a <see cref="LeanTouchMessage"/> is received.
        /// </summary>
        public event Action<LeanTouchMessage> LeanTouchReceived = delegate { };

        /// <summary>
        /// Called on server and on device client for every <see cref="LeanTouchMessage.FingersDown"/> in a received message.
        /// </summary>
        public event Action<int, LeanFingerInfo> OnFingerDown = delegate { };

        /// <summary>
        /// Called on server and on device client for every <see cref="LeanTouchMessage.FingersSet"/> in a received message.
        /// </summary>
        public event Action<int, LeanFingerInfo> OnFingerSet = delegate { };

        /// <summary>
        /// Called on server and on device client for every <see cref="LeanTouchMessage.FingersUp"/> in a received message.
        /// </summary>
        public event Action<int, LeanFingerInfo> OnFingerUp = delegate { };

        /// <summary>
        /// Called on server and on device client for every <see cref="LeanTouchMessage.FingersTap"/> in a received message.
        /// </summary>
        public event Action<int, LeanFingerInfo> OnFingerTap = delegate { };

        /// <summary>
        /// Called on server and device client for every <see cref="LeanTouchMessage.FingersSwipe"/> in a received message.
        /// </summary>
        public event Action<int, LeanFingerInfo> OnFingerSwipe = delegate { };

        /// <summary>
        /// Called on server and device client for a non-empty <see cref="LeanTouchMessage.Gestures"/> list in a received message.
        /// </summary>
        public event Action<int, List<LeanFingerInfo>> OnGesture = delegate { };

        // Variables

        protected bool initialAutoStartSending;
        protected LeanTouchInfoMessage leanTouchInfoMessage = new LeanTouchInfoMessage();
        protected LeanTouchMessage leanTouchMessage = new LeanTouchMessage();
        protected bool lastLeanTouchMessageEmpty = false;

        // Methods

        /// <summary>
        /// Initializes the properties and susbcribes to events.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            LeanTouchesInfo = new Dictionary<int, LeanTouchInfoMessage>();
            LeanTouches = new Dictionary<int, LeanTouchMessage>();

            DeviceConnected += DevicesInfoSync_DeviceConnected;
            DeviceDisconnected += DevicesInfoSync_DeviceDisconnected;

            if (LogFilter.logInfo)
            {
                OnFingerDown  += (clientId, finger) => { UnityEngine.Debug.Log("LeanTouchSync: finger " + finger.Index + " down on client " + clientId); };
                OnFingerSet   += (clientId, finger) => { UnityEngine.Debug.Log("LeanTouchSync: finger " + finger.Index + " set on client " + clientId); };
                OnFingerUp    += (clientId, finger) => { UnityEngine.Debug.Log("LeanTouchSync: finger " + finger.Index + " up on client " + clientId); };
                OnFingerTap   += (clientId, finger) => { UnityEngine.Debug.Log("LeanTouchSync: finger " + finger.Index + " tap on client " + clientId); };
                OnFingerSwipe += (clientId, finger) => { UnityEngine.Debug.Log("LeanTouchSync: finger " + finger.Index + " swipe on client " + clientId); };
            }

            MessageTypes.Add(leanTouchInfoMessage.MessageType);
            MessageTypes.Add(leanTouchMessage.MessageType);
        }

        /// <summary>
        /// Starts capturing the <see cref="Lean.Touch.LeanTouch"/> events and sends a <see cref="LeanTouchInfoMessage"/> to server.
        /// </summary>
        protected override void Start()
        {
            base.Start();

            leanTouchMessage.SetCapturingEvents(true);

            leanTouchInfoMessage.Update();
            SendToServer(leanTouchInfoMessage, Channels.DefaultReliable);
        }

        /// <summary>
        /// Unsubscribes to events and stop capturing the LeanTouch events.
        /// </summary>
        protected virtual void OnDestroy()
        {
            leanTouchMessage.SetCapturingEvents(false);

            DeviceConnected -= DevicesInfoSync_DeviceConnected;
            DeviceDisconnected -= DevicesInfoSync_DeviceDisconnected;
        }

        /// <summary>
        /// Updates a <see cref="LeanTouchMessage"/>, and sends it if required and <see cref="LeanTouchMessage.Fingers"/>
        /// is not empty.
        /// </summary>
        /// <param name="sendToServerThisFrame">If the message should be sent this frame.</param>
        protected override void OnSendToServerIntervalIteration(bool sendToServerThisFrame)
        {
            if (sendToServerThisFrame)
            {
                leanTouchMessage.Update();

                bool emptyLeanTouchMessage = leanTouchMessage.Fingers.Length == 0;
                if (!emptyLeanTouchMessage || !lastLeanTouchMessageEmpty)
                {
                    SendToServer(leanTouchMessage);
                    leanTouchMessage.Reset();
                }
                lastLeanTouchMessageEmpty = emptyLeanTouchMessage;
            }
        }

        /// <summary>
        /// For <see cref="LeanTouchInfoMessage"/>, .
        /// For <see cref="LeanTouchMessage"/>, calls <see cref="ProcessLeanTouchMessage"/>.
        /// </summary>
        /// <param name="netMessage">The received networking message.</param>
        /// <returns>The typed network message extracted.</returns>
        protected override DevicesSyncMessage OnServerMessageReceived(NetworkMessage netMessage)
        {
            if (netMessage.msgType == leanTouchMessage.MessageType)
            {
                var leanTouchMessage = netMessage.ReadMessage<LeanTouchMessage>();
                ProcessLeanTouchMessage(leanTouchMessage);
                return leanTouchMessage;
            }
            else if (netMessage.msgType == leanTouchInfoMessage.MessageType)
            {
                var leanTouchInfoMessage = netMessage.ReadMessage<LeanTouchInfoMessage>();
                LeanTouchesInfo[leanTouchInfoMessage.SenderConnectionId] = leanTouchInfoMessage;
                LeanTouchInfoReceived.Invoke(leanTouchInfoMessage);
                return leanTouchInfoMessage;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// For <see cref="LeanTouchInfoMessage"/>, updates <see cref="LeanTouchesInfo"/> and start sending
        /// <see cref="LeanTouchMessage"/> as the server has received LeanTouchInfoMessage.
        /// For <see cref="LeanTouchMessage"/>, calls <see cref="ProcessLeanTouchMessage"/>.
        /// </summary>
        /// <param name="netMessage">The received networking message.</param>
        /// <returns>The typed network message extracted.</returns>
        protected override DevicesSyncMessage OnClientMessageReceived(NetworkMessage netMessage)
        {
            if (netMessage.msgType == leanTouchMessage.MessageType)
            {
                var leanTouchMessage = netMessage.ReadMessage<LeanTouchMessage>();
                if (!isServer)
                {
                    ProcessLeanTouchMessage(leanTouchMessage);
                }
                return leanTouchMessage;
            }
            else if (netMessage.msgType == leanTouchInfoMessage.MessageType)
            {
                var leanTouchInfoMessage = netMessage.ReadMessage<LeanTouchInfoMessage>();
                if (!isServer)
                {
                    LeanTouchesInfo[leanTouchInfoMessage.SenderConnectionId] = leanTouchInfoMessage;
                    LeanTouchInfoReceived.Invoke(leanTouchInfoMessage);
                }
                return leanTouchInfoMessage;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Server sends to the new device client the LeanTouch information from all the currently connected devices.
        /// </summary>
        /// <param name="deviceId">The new device client id.</param>
        protected virtual void DevicesInfoSync_DeviceConnected(int deviceId)
        {
            if (isServer)
            {
                foreach (var leanTouchInfo in LeanTouchesInfo)
                {
                    SendToClient(deviceId, leanTouchInfo.Value);
                }
            }
        }

        /// <summary>
        /// Removes the disconnected device from <see cref="LeanTouchesInfo"/> and <see cref="LeanTouches"/>.
        /// </summary>
        /// <param name="deviceId">The id of the disconnected device.</param>
        protected virtual void DevicesInfoSync_DeviceDisconnected(int deviceId)
        {
            LeanTouchesInfo.Remove(deviceId);
            LeanTouches.Remove(deviceId);
        }

        /// <summary>
        /// Updates <see cref="LeanTouches"/> and invokes <see cref="LeanTouchReceived"/>, fingers and gestures related
        /// events.
        /// </summary>
        protected virtual void ProcessLeanTouchMessage(LeanTouchMessage leanTouchMessage)
        {
            int senderDeviceId = leanTouchMessage.SenderConnectionId;
            if (LeanTouchesInfo.ContainsKey(senderDeviceId))
            {
                leanTouchMessage.Restore(LeanTouchesInfo[senderDeviceId]);
            }

            LeanTouches[senderDeviceId] = leanTouchMessage;
            LeanTouchReceived.Invoke(leanTouchMessage);

            if (leanTouchMessage.Fingers.Length > 0)
            {
                var fingerEvents = new List<Tuple<LeanFingerInfo[], Action<int, LeanFingerInfo>>>
                {
                    new Tuple<LeanFingerInfo[], Action<int, LeanFingerInfo>>(leanTouchMessage.FingersDown, OnFingerDown),
                    new Tuple<LeanFingerInfo[], Action<int, LeanFingerInfo>>(leanTouchMessage.FingersSet, OnFingerSet),
                    new Tuple<LeanFingerInfo[], Action<int, LeanFingerInfo>>(leanTouchMessage.FingersUp, OnFingerUp),
                    new Tuple<LeanFingerInfo[], Action<int, LeanFingerInfo>>(leanTouchMessage.FingersTap, OnFingerTap),
                    new Tuple<LeanFingerInfo[], Action<int, LeanFingerInfo>>(leanTouchMessage.FingersSwipe, OnFingerSwipe)
                };

                foreach (var fingerEvent in fingerEvents)
                {
                    foreach (var finger in fingerEvent.Item1)
                    {
                        fingerEvent.Item2.Invoke(senderDeviceId, finger);
                    }
                }
                OnGesture.Invoke(senderDeviceId, new List<LeanFingerInfo>(leanTouchMessage.Gestures));
            }
        }
    }
}
