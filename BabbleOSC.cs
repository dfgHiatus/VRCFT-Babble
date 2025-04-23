using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using VRCFaceTracking.Core.OSC;

namespace VRCFaceTracking.Babble;

public class BabbleOSC
{
    private Socket? _receiver;
    private bool _loop = true;
    private readonly Thread _thread;
    private readonly ILogger _logger;
    private readonly int _resolvedPort;
    private readonly string _resolvedHost;

    public const string DEFAULT_HOST = "127.0.0.1";
    public const int DEFAULT_PORT = 8888;
    private const int TIMEOUT_MS = 10000;

    public BabbleOSC(ILogger logger, string? host = null, int? port = null)
    {
        _logger = logger;

        if (_receiver != null)
        {
            _logger.LogError("BabbleOSC connection already exists.");
            return;
        }

        _resolvedHost = host ?? DEFAULT_HOST;
        _resolvedPort = port ?? DEFAULT_PORT;

        _logger.LogInformation($"Started BabbleOSC with Host: {_resolvedHost} and Port {_resolvedPort}");

        ConfigureReceiver();
        _loop = true;
        _thread = new Thread(ListenLoop);
        _thread.Start();
    }

    private void ConfigureReceiver()
    {
        IPAddress address = IPAddress.Parse(_resolvedHost);
        IPEndPoint localEP = new IPEndPoint(address, _resolvedPort);
        _receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _receiver.Bind(localEP);
        _receiver.ReceiveTimeout = TIMEOUT_MS;
    }

    private void ListenLoop()
    {
        byte[] buffer = new byte[4096];

        while (_loop)
        {
            try
            {
                if (_receiver != null && _receiver.IsBound)
                {
                    int len = _receiver.Receive(buffer);
                    int messageIndex = 0;

                    try
                    {
                        OscMessage oscMessage = new OscMessage(buffer, len, ref messageIndex);
                        ProcessMessage(oscMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing OSC message.");
                        continue;
                    }
                }
                else
                {
                    _receiver?.Close();
                    _receiver?.Dispose();
                    ConfigureReceiver();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in Babble OSC receive loop.");
            }
        }
    }

    private void ProcessMessage(OscMessage oscMessage)
    {
        if (oscMessage.Value is float value)
        {
            // Scale expressions that are mapped to several expressions
            // IE mouthFunnel maps to LipFunnelLowerLeft, LipFunnelLowerRight, LipFunnelUpperLeft, LipFunnelUpperRight
            // IE 2: mouthLeft maps to MouthUpperLeft and MouthLowerLeft
            switch (oscMessage.Address)
            {
                case "/mouthFunnel":
                case "/mouthPucker":
                    BabbleExpressions.BabbleExpressionMap.SetByKey2(oscMessage.Address, value * 4f);
                    break;
                case "/mouthLeft":
                case "/mouthRight":
                    BabbleExpressions.BabbleExpressionMap.SetByKey2(oscMessage.Address, value * 2f);
                    break;
                default:
                    if (BabbleExpressions.BabbleExpressionMap.ContainsKey2(oscMessage.Address))
                    {
                        BabbleExpressions.BabbleExpressionMap.SetByKey2(oscMessage.Address, value);
                    }
                    break;
            }
        }
    }

    public void Teardown()
    {
        _loop = false;
        _receiver?.Close();
        _receiver?.Dispose();
        _thread.Join();
    }
}