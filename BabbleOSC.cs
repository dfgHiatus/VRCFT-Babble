using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
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

    private const string DEFAULT_HOST = "127.0.0.1";
    private const int DEFAULT_PORT = 8888;

    private const int TIMEOUT_MS = 10000;

    public BabbleOSC(ILogger iLogger, int? port = null, string? host = null)
    {
        _logger = iLogger;
        if (_receiver != null)
        {
            _logger.LogError("BabbleOSC connection already exists.");
            return;
        }

        _resolvedHost = host ?? DEFAULT_HOST;
        _resolvedPort = port ?? DEFAULT_PORT;

        ConfigureReceiver();

        _loop = true;
        _thread = new Thread(ListenLoop);
        _thread.Start();
    }

    private void ConfigureReceiver()
    {
        var ipAddress = IPAddress.Parse(_resolvedHost);
        var ipEndPoint = new IPEndPoint(ipAddress, _resolvedPort);
            
        _receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _receiver.Bind(ipEndPoint);
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
                    int num = _receiver.Receive(array);
                    int num2 = 0;
                    OscMessage val;
                    try
                    {
                        val = new OscMessage(array, num, ref num2);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (val.Value is float)
                    {
                        if (val.Address == "/mouthFunnel" || val.Address == "/mouthPucker")
                        {
                            BabbleExpressions.BabbleExpressionMap.SetByKey2(val.Address, (float)val.Value * 4f);
                        }
                        else if (BabbleExpressions.BabbleExpressionMap.ContainsKey2(val.Address))
                        {
                            BabbleExpressions.BabbleExpressionMap.SetByKey2(val.Address, (float)val.Value);
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
