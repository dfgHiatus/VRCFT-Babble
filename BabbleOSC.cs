using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using VRCFaceTracking.Core.OSC;

namespace VRCFaceTracking.Babble;

public class BabbleOSC
{
    private Socket _receiver;

    private bool _loop = true;

    private readonly Thread _thread;

    private readonly ILogger _logger;

    private readonly int _resolvedPort;

    private readonly string _resolvedHost;

    public const string DEFAULT_HOST = "127.0.0.1";

    public const int DEFAULT_PORT = 8888;

    private const int TIMEOUT_MS = 10000;

    public BabbleOSC(ILogger iLogger, string? host = null, int? port = null)
    {
        _logger = iLogger;
        if (_receiver != null)
        {
            _logger.LogError("BabbleOSC connection already exists.");
            return;
        }
        _resolvedHost = host ?? DEFAULT_HOST;
        _resolvedPort = port ?? TIMEOUT_MS;

        _logger.LogInformation($"Started BabbleOSC with Host: {_resolvedHost} and Port {_resolvedPort}");
        ConfigureReceiver();
        _loop = true;
        _thread = new Thread(new ThreadStart(ListenLoop));
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
        byte[] array = new byte[4096];
        while (_loop)
        {
            try
            {
                if (_receiver.IsBound)
                {
                    int len = _receiver.Receive(array);
                    int messageIndex = 0;
                    OscMessage oscMessage;
                    try
                    {
                        oscMessage = new OscMessage(array, len, ref messageIndex);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (oscMessage.Value is float)
                    {
                        if (oscMessage.Address == "/mouthFunnel" || oscMessage.Address == "/mouthPucker")
                        {
                            BabbleExpressions.BabbleExpressionMap.SetByKey2(oscMessage.Address, (float)oscMessage.Value * 4f);
                        }
                        else if (BabbleExpressions.BabbleExpressionMap.ContainsKey2(oscMessage.Address))
                        {
                            BabbleExpressions.BabbleExpressionMap.SetByKey2(oscMessage.Address, (float)oscMessage.Value);
                        }
                    }
                }
                else
                {
                    _receiver.Close();
                    _receiver.Dispose();
                    ConfigureReceiver();
                }
            }
            catch (Exception)
            {
            }
        }
    }

    public void Teardown()
    {
        _loop = false;
        _receiver.Close();
        _receiver.Dispose();
        _thread.Join();
    }
}
