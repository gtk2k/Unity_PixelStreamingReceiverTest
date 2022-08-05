using System.Collections;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

public class PixelStreamingReceiver : MonoBehaviour
{
    [SerializeField] private RawImage display;

    private PixelStreamingSignalingClient signaling;
    private RTCPeerConnection pc;
    private RTCDataChannel dc;
    private MediaStream receiveVideoStream;

    private void Start()
    {
        WebRTC.Initialize();// enableNativeLog:true, nativeLoggingSeverity: NativeLoggingSeverity.INFO);
        StartCoroutine(WebRTC.Update());

        signaling = new PixelStreamingSignalingClient("ws://localhost");
        signaling.OnConfig += Signaling_OnConfig;
        signaling.OnPlayerCount += Signaling_OnPlayerCount;
        signaling.OnOffer += Signaling_OnOffer;
        signaling.OnIceCandidate += Signaling_OnIceCandidate;
        signaling.Connect();
    }

    private void OnApplicationQuit()
    {
        if(signaling != null)
        {
            signaling.Close();
            signaling = null;
        }
    }

    private void Signaling_OnConfig(RTCConfiguration config)
    {
        Debug.Log($"OnConfig");

        pc = new RTCPeerConnection(ref config);
        pc.OnIceCandidate = candidate =>
        {
            signaling.SendIceCandidate(candidate);
        };
        pc.OnIceGatheringStateChange = state =>
        {
            Debug.Log($"OnIceGatheringStateChange > {state}");
        };
        pc.OnConnectionStateChange = state =>
        {
            Debug.Log($"OnConnectionStateChange > {state}");
        };
        pc.OnTrack = evt =>
        {
            Debug.Log($"OnTrack");

            if (evt.Track is VideoStreamTrack videoTrack)
            {
                Debug.Log($"OnVideoTrack");

                videoTrack.OnVideoReceived += (tex) =>
                {
                    display.texture = tex;
                };
                receiveVideoStream = evt.Streams.First();
                receiveVideoStream.OnRemoveTrack = (e) =>
                {
                    display.texture = null;
                    e.Track.Dispose();
                };
            }
        };
        pc.OnDataChannel = dc =>
        {
            Debug.Log($"OnDataChannel > {dc.Label}");
        };
    }

    private void Signaling_OnPlayerCount(int count)
    {
        Debug.Log($"OnPlayerCount > count: {count}");
    }

    private void Signaling_OnOffer(RTCSessionDescription offer)
    {
        Debug.Log($"OnOffer");
        //offer.sdp = offer.sdp.Replace("42e01f", "42e033");
        StartCoroutine(SetDescription(offer));
    }

    private void Signaling_OnIceCandidate(RTCIceCandidate candidate)
    {
        Debug.Log($"OnIceCandidate");

        pc.AddIceCandidate(candidate);
    }

    private IEnumerator SetDescription(RTCSessionDescription desc)
    {
        var op = desc.type == RTCSdpType.Offer ? pc.SetRemoteDescription(ref desc) : pc.SetLocalDescription(ref desc);
        yield return op;
        if (op.IsError)
        {
            Debug.LogError($"Set {desc.type} Error > {op.Error.message}");
            yield break;
        }
        if(desc.type == RTCSdpType.Offer)
        {
            yield return StartCoroutine(CreateAnswer());
        }
        else
        {
            signaling.SendAnswer(desc);
        }
    }

    private IEnumerator CreateAnswer()
    {
        Debug.Log($"CreateAnswer");

        var op = pc.CreateAnswer();
        yield return op;
        if (op.IsError)
        {
            Debug.LogError($"CreateAnswer Error > {op.Error.message}");
        }
        yield return StartCoroutine(SetDescription(op.Desc));
    }
}
