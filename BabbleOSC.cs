using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VRCFaceTracking.Core.OSC;

namespace VRCFaceTracking.Babble;
public partial class BabbleOSC
{
    private Socket _receiver;
    private bool _loop = true;
    private readonly Thread _thread;
    private readonly ILogger _logger;
    private readonly int _resolvedPort;
    private const int DEFAULT_PORT = 8888;
    private const int TIMEOUT_MS = 10_000;

    public BabbleOSC(ILogger iLogger, int? port = null)
    {   
        _logger = iLogger;

        if (_receiver != null)
        {
            _logger.LogError("BabbleOSC connection already exists.");
            return;
        }

        _receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _resolvedPort = port ?? DEFAULT_PORT;
        _receiver.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), _resolvedPort));
        _receiver.ReceiveTimeout = TIMEOUT_MS;

        _loop = true;
        _thread = new Thread(new ThreadStart(ListenLoop));
        _thread.Start();
    }

    struct Msg
    {
        public string address;
        public float value;
        public bool success;
    }

    // https://github.com/benaclejames/SimpleRustOSC/blob/master/src/lib.rs#L54
    private Msg ParseOSC(byte[] buffer, int length)
    {
        Msg msg = new Msg();
        msg.success = false;

        if (length < 4)
            return msg;

        int bufferPosition = 0;
        string address = ParseString(buffer, length, ref bufferPosition);
        if (address == "")
            return msg;

        msg.address = address;

        // checking for ',' char
        if (buffer[bufferPosition] != 44)
            return msg;
        bufferPosition++; // skipping ',' character

        char valueType = (char)buffer[bufferPosition]; // unused
        bufferPosition++;

        float value = ParesFloat(buffer, length, bufferPosition);

        msg.value = value;
        msg.success = true;

        return msg;
    }

    private string ParseString(byte[] buffer, int length, ref int bufferPosition)
    {
        string address = "";

        // first character must be '/'
        if (buffer[0] != 47)
            return address;

        for (int i = 0; i < length; i++)
        {
            if (buffer[i] == 0)
            {
                bufferPosition = i + 1;

                if (bufferPosition % 4 != 0)
                {
                    bufferPosition += 4 - (bufferPosition % 4);
                }

                break;
            }

            address += (char)buffer[i];
        }

        return address;
    }

    private float ParesFloat(byte[] buffer, int length, int bufferPosition)
    {
        var valueBuffer = new byte[length - bufferPosition];

        int j = 0;
        for (int i = bufferPosition; i < length; i++)
        {
            valueBuffer[j] = buffer[i];

            j++;
        }

        float value = bytesToFLoat(valueBuffer);
        return value;
    }

    private float bytesToFLoat(byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes); // Convert big endian to little endian
        }

        float myFloat = BitConverter.ToSingle(bytes, 0);
        return myFloat;
    }

    private void ListenLoop()
    {
        var buffer = new byte[4096];

        while (_loop)
        {
            try
            {
                if (_receiver.IsBound)
                {
                    var length = _receiver.Receive(buffer);

                    Msg msg = ParseOSC(buffer, length);
                    if (msg.success && BabbleExpressionMap.ContainsKey(msg.address))
                    {
                        BabbleExpressionMap[msg.address] = msg.value;
                    }
                }
                else
                {
                    _receiver.Close();
                    _receiver.Dispose();
                    _receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    _receiver.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), _resolvedPort));
                    _receiver.ReceiveTimeout = TIMEOUT_MS;
                }
            }
            catch (Exception) { }
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
