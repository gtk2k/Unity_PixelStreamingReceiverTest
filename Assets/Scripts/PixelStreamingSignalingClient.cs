using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using WebSocketSharp;

public class PixelStreamingSignalingClient
{
    public event Action OnOpen;
    public event Action<RTCConfiguration> OnConfig;
    public event Action<int> OnPlayerCount;
    public event Action<RTCSessionDescription> OnOffer;
    public event Action<RTCIceCandidate> OnIceCandidate;
    public event Action<ushort, string> OnClose;
    public event Action<Exception> OnError;

    private SynchronizationContext ctx;

    private WebSocket ws;

    private JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore
    };


    [Serializable]
    private class IceServer
    {
        public string[] urls;
    }

    [Serializable]
    private class PeerConnectionOptions
    {
        public IceServer[] iceServers;
    }

    [Serializable]
    private class IceCandidate
    {
        public string candidate;
        public string sdpMid;
        public int? sdpMLineIndex;
    }

    private class SignalingMessage
    {
        public string type;
        public string sdp;
        public int count;
        public PeerConnectionOptions peerConnectionOptions;
        public IceCandidate candidate;
    }

    public PixelStreamingSignalingClient(string url)
    {
        ctx = SynchronizationContext.Current;

        ws = new WebSocket(url);
        ws.OnOpen += Ws_OnOpen;
        ws.OnMessage += Ws_OnMessage;
        ws.OnClose += Ws_OnClose;
        ws.OnError += Ws_OnError;
    }

    private void Ws_OnOpen(object sender, EventArgs e)
    {
        ctx.Post(_ =>
        {
            OnOpen?.Invoke();
        }, null);
    }

    private void Ws_OnMessage(object sender, MessageEventArgs e)
    {
        ctx.Post(_ =>
        {
            Debug.Log($"Receive <== {e.Data}");

            var msg = JsonConvert.DeserializeObject<SignalingMessage>(e.Data);
            switch (msg.type)
            {
                case "config":
                    var cfg = msg.peerConnectionOptions;
                    var iceServers = cfg.iceServers.Select(x => new RTCIceServer { urls = x.urls }).ToArray();
                    var config = new RTCConfiguration { iceServers = iceServers };
                    OnConfig?.Invoke(config);
                    break;
                case "playerCount":
                    OnPlayerCount?.Invoke(msg.count);
                    break;
                case "offer":
                    var offer = new RTCSessionDescription
                    {
                        type = RTCSdpType.Offer,
                        sdp = msg.sdp
                    };
                    OnOffer?.Invoke(offer);
                    break;
                case "iceCandidate":
                    var c = msg.candidate;
                    var iceCandidate = new RTCIceCandidate(new RTCIceCandidateInit
                    {
                        candidate = c.candidate,
                        sdpMid = c.sdpMid,
                        sdpMLineIndex = c.sdpMLineIndex
                    });
                    OnIceCandidate?.Invoke(iceCandidate);
                    break;
            }
        }, null);
    }

    private void Ws_OnClose(object sender, CloseEventArgs e)
    {
        ctx.Post(_ =>
        {
            Debug.Log($"Ws_OnClose > code: {e.Code}, reason: {e.Reason}");
        }, null);
    }

    private void Ws_OnError(object sender, ErrorEventArgs e)
    {
        ctx.Post(_ =>
        {
            Debug.LogError(e.Message);
        }, null);
    }

    public void Connect()
    {
        ws.Connect();
    }

    public void Close()
    {
        if (ws != null)
        {
            if (ws.ReadyState == WebSocketState.Open)
            {
                ws.Close();
            }
            ws = null;
        }
    }

    public void SendAnswer(RTCSessionDescription answer)
    {
        var msg = new SignalingMessage
        {
            type = "answer",
            sdp = answer.sdp
        };
        Send(msg);
    }

    public void SendIceCandidate(RTCIceCandidate c)
    {
        var msg = new SignalingMessage
        {
            type = "iceCandidate",
            candidate = new IceCandidate
            {
                candidate = c.Candidate,
                sdpMid = c.SdpMid,
                sdpMLineIndex = c.SdpMLineIndex
            }
        };
        Send(msg);
    }

    private void Send(SignalingMessage msg)
    {
        var data = JsonConvert.SerializeObject(msg);
        Debug.Log($"Send ==> {data}");
        ws.Send(data);
    }
}
