using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class ConnectionManager : MonoBehaviour
    {
        private class UnpackException : Exception
        {
            public UnpackException(string message)
                : base(message)
            {
            }

            public UnpackException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        public struct Package
        {
            public string ChannelName;

            public byte PkgType;

            // timestamp in system byte order
            public double Timestamp;

            // payload in system byte order
            public byte[] Payload;
        }

        [SerializeField] private string _hostName;
        [SerializeField] private string _port;

        // List of input channels from which we want to receive data
        [SerializeField] private List<string> _expectedChannels;

#if NETFX_CORE || ENABLE_WINMD_SUPPORT
    private Windows.Networking.Sockets.StreamSocket _socket;
    private Windows.Networking.HostName _serverHost;
#endif
        private Task _listeningTask;

        private NtpSync _ntpSync;
        private Stream _streamOut;
        private Stream _streamIn;

        private Dictionary<string, ConcurrentQueue<Package>> _channelBuffers;
        readonly string UNKOWN_CHANNEL = "UNKNOWN";

        readonly long TICKS_OF_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        public const byte BYTE_TYPE = 0x00;
        public const byte CHAR_TYPE = 0x01;
        public const byte SHORT_TYPE = 0x02;
        public const byte INT_TYPE = 0x03;
        public const byte LONG_TYPE = 0x04; // type long is currently bugged in lsl so please avoid using it 
        public const byte FLOAT_TYPE = 0x05;
        public const byte DOUBLE_TYPE = 0x06;
        public const byte EMPTY = 0x07;

        // all package sizes are in bytes
        const int TIMESTAMP_SIZE = 8;
        const int TYPE_SIZE = 1;
        const int SIZE_OF_NAME_SIZE = 4;
        const int SIZE_OF_PAYLOAD_SIZE = 4;

        const int BYTE_SIZE_SHORT = 2;
        const int BYTE_SIZE_INT = 4;
        const int BYTE_SIZE_FLOAT = 4;
        const int BYTE_SIZE_DOUBLE = 8;

        private bool _noConnection;

        readonly Package EMPTY_PKG = new Package
            {ChannelName = "", PkgType = 0x07, Timestamp = -1, Payload = new byte[0]};

        private void Awake()
        {
            _channelBuffers = new Dictionary<string, ConcurrentQueue<Package>>();
        }

        // Use this for initialization
        private void Start()
        {
            _ntpSync = GetComponent<NtpSync>();
            if (_ntpSync == null)
            {
                Debug.LogError("NtpSync NULL.");
                
            }
            foreach (var channelName in _expectedChannels)
            {
                _channelBuffers.Add(channelName, new ConcurrentQueue<Package>());
            }

            _channelBuffers.Add(UNKOWN_CHANNEL, new ConcurrentQueue<Package>());

            _noConnection = true;
            Connect();
        }

        private async void Connect()
        {
            try
            {
#if NETFX_CORE || ENABLE_WINMD_SUPPORT
        // Use Windows.Networking.Sockets.StreamSocket for Windows-based platforms
        _socket = new Windows.Networking.Sockets.StreamSocket();
        Windows.Networking.HostName serverHost = new Windows.Networking.HostName(_hostName);
        await _socket.ConnectAsync(serverHost, _port);

        // Convert the stream to a standard .NET stream for ease of use
        _streamOut = _socket.OutputStream.AsStreamForWrite();
        _streamIn = _socket.InputStream.AsStreamForRead();
        _noConnection = false;

        // Start listening for incoming data
        StartListening();

#elif !NETFX_CORE && !ENABLE_WINMD_SUPPORT
                // For non-Windows platforms, use TcpClient
                TcpClient client = new TcpClient(_hostName, int.Parse(_port));
                _streamOut = client.GetStream();
                _streamIn = client.GetStream();

                _noConnection = false;
                StartListening();
                Debug.Log("TCP CONNECTION MADE");

#endif
            }
            catch (Exception e)
            {
                // Log any other errors
                Debug.LogError($"Connection Failed: {e.Message}");
            }
        }



        private void StartListening()
        {
            if (_listeningTask != null)
            {
                StopListening();
            }

            _listeningTask = Task.Run(() => RunListening());
        }

        private byte[] PackBigEndianAsciiString(string value)
        {
            // Already in big endian order...
            var packedString = Encoding.ASCII.GetBytes(value);

            return packedString;
        }

        static byte[] ConvertByteOrderWithPackageTypeCheck(byte[] payload, byte packageType, byte expectedType)
        {
            if (packageType != expectedType)
            {
                throw new UnpackException(
                    $"Package type does not match the return type! Package type {packageType} is not {expectedType} return type.");
            }

            return payload;
        }

        #region Unpacking

        public static string UnpackString(Package pkg)
        {
            var payload = ConvertByteOrderWithPackageTypeCheck(pkg.Payload, pkg.PkgType, CHAR_TYPE);

            return Encoding.ASCII.GetString(payload);
        }

        public static byte[] UnpackByte(Package pkg)
        {
            var payload = ConvertByteOrderWithPackageTypeCheck(pkg.Payload, pkg.PkgType, BYTE_TYPE);

            return payload;
        }

        public static short[] UnpackShort(Package pkg)
        {
            var payload = ConvertByteOrderWithPackageTypeCheck(pkg.Payload, pkg.PkgType, SHORT_TYPE);

            var valueCount = payload.Length / BYTE_SIZE_SHORT;
            var values = new short[valueCount];
            for (var i = 0; i < valueCount; i++)
            {
                var valueInByte = payload.Skip(i * BYTE_SIZE_SHORT).Take(BYTE_SIZE_SHORT).ToArray();
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valueInByte);
                }

                values[i] = BitConverter.ToInt16(valueInByte, 0);
            }

            return values;
        }

        public static int[] UnpackInt(Package pkg)
        {
            var payload = ConvertByteOrderWithPackageTypeCheck(pkg.Payload, pkg.PkgType, INT_TYPE);

            var valueCount = payload.Length / BYTE_SIZE_INT;
            var values = new int[valueCount];
            for (var i = 0; i < valueCount; i++)
            {
                var valueInByte = payload.Skip(i * BYTE_SIZE_INT).Take(BYTE_SIZE_INT).ToArray();
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valueInByte);
                }

                values[i] = BitConverter.ToInt32(valueInByte, 0);
            }

            return values;
        }

        public static float[] UnpackFloat(Package pkg)
        {
            var payload = ConvertByteOrderWithPackageTypeCheck(pkg.Payload, pkg.PkgType, FLOAT_TYPE);

            var valueCount = payload.Length / BYTE_SIZE_FLOAT;
            var values = new float[valueCount];
            for (var i = 0; i < valueCount; i++)
            {
                var valueInByte = payload.Skip(i * BYTE_SIZE_FLOAT).Take(BYTE_SIZE_FLOAT).ToArray();
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valueInByte);
                }

                values[i] = BitConverter.ToSingle(valueInByte, 0);
            }

            return values;
        }

        public static double[] UnpackDouble(Package pkg)
        {
            var payload = ConvertByteOrderWithPackageTypeCheck(pkg.Payload, pkg.PkgType, DOUBLE_TYPE);

            var valueCount = payload.Length / BYTE_SIZE_DOUBLE;
            var values = new double[valueCount];
            for (var i = 0; i < valueCount; i++)
            {
                var valueInByte = payload.Skip(i * BYTE_SIZE_DOUBLE).Take(BYTE_SIZE_DOUBLE).ToArray();
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valueInByte);
                }

                values[i] = BitConverter.ToDouble(valueInByte, 0);
            }

            return values;
        }

        #endregion

        #region Unpacking

        public void Send(string message, string channelName)
        {
            var messageInByte = PackBigEndianAsciiString(message);
            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, CHAR_TYPE);

        }

        public void Send(byte message, string channelName)
        {
            var messageInByte = new[] {message};
            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, BYTE_TYPE);
        }

        public void Send(byte[] message, string channelName)
        {
            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(message, channelNameInByte, BYTE_TYPE);
        }

        public void Send(Int16 message, string channelName)
        {
            var messageInByte = BitConverter.GetBytes(message);
            if (BitConverter.IsLittleEndian) Array.Reverse(messageInByte);

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, SHORT_TYPE);
        }

        public void Send(Int16[] message, string channelName)
        {
            var messageInByte = new byte[message.Length * BYTE_SIZE_SHORT];
            for (var i = 0; i < message.Length; i++)
            {
                var value = BitConverter.GetBytes(message[i]);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }

                value.CopyTo(messageInByte, i * BYTE_SIZE_SHORT);
            }

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, SHORT_TYPE);
        }

        public void Send(Int32 message, string channelName)
        {
            var messageInByte = BitConverter.GetBytes(message);
            if (BitConverter.IsLittleEndian) Array.Reverse(messageInByte);

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, INT_TYPE);
        }

        public void Send(Int32[] message, string channelName)
        {
            var messageInByte = new byte[message.Length * BYTE_SIZE_INT];
            for (var i = 0; i < message.Length; i++)
            {
                var value = BitConverter.GetBytes(message[i]);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }

                value.CopyTo(messageInByte, i * BYTE_SIZE_INT);
            }

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, INT_TYPE);
        }

        public void Send(float message, string channelName)
        {
            var messageInByte = BitConverter.GetBytes(message);
            if (BitConverter.IsLittleEndian) Array.Reverse(messageInByte);

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, FLOAT_TYPE);
        }

        public void Send(float[] message, string channelName)
        {
            var messageInByte = new byte[message.Length * BYTE_SIZE_FLOAT];
            for (var i = 0; i < message.Length; i++)
            {
                var value = BitConverter.GetBytes(message[i]);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }

                value.CopyTo(messageInByte, i * BYTE_SIZE_FLOAT);
            }

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, FLOAT_TYPE);
        }

        public void Send(double message, string channelName)
        {
            var messageInByte = BitConverter.GetBytes(message);
            if (BitConverter.IsLittleEndian) Array.Reverse(messageInByte);

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, DOUBLE_TYPE);
        }

        public void Send(double[] message, string channelName)
        {
            var messageInByte = new byte[message.Length * BYTE_SIZE_DOUBLE];
            for (var i = 0; i < message.Length; i++)
            {
                var value = BitConverter.GetBytes(message[i]);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }

                value.CopyTo(messageInByte, i * BYTE_SIZE_DOUBLE);
            }

            var channelNameInByte = PackBigEndianAsciiString(channelName);

            SendPackage(messageInByte, channelNameInByte, DOUBLE_TYPE);
        }

#pragma warning disable AsyncFixer01 // Unnecessary async/await usage
        async void SendPackage(byte[] payload, byte[] channelName, byte type)
        {
            if (_noConnection) return;

            if (_streamOut == null)
            {
                Debug.LogError("Stream out is not initialized.");
                return;
            }

            if (_ntpSync == null)
            {
                Debug.LogError("NtpSync is not initialized.");
                return;
            }

            if (payload == null || channelName == null)
            {
                Debug.LogError("Payload or channelName is null.");
                return;
            }

            // Get timestamp from the 1st of January 1970
            var timestamp = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - TICKS_OF_EPOCH).TotalSeconds -
                            _ntpSync.NtpTimeOffset;
            var networkTimestamp = BitConverter.GetBytes(timestamp);
            var payloadSize = BitConverter.GetBytes((uint) payload.Length);
            var channelNameSize = BitConverter.GetBytes((uint) channelName.Length);


            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(networkTimestamp);
                Array.Reverse(payloadSize);
                Array.Reverse(channelNameSize);
            }

            var completePackageSize = TIMESTAMP_SIZE + TYPE_SIZE + SIZE_OF_NAME_SIZE + channelName.Length +
                                      SIZE_OF_PAYLOAD_SIZE + payload.Length;

            /**
            Protocol:
         
            64 Bit time stamp, 
            8 Bit lsl format, 
            32 Bit channel name size,
            n Bit channel name,
            32 Bit payload size,
            n Bit payload 
            */
            var package = new byte[completePackageSize];

            // Copy data to the package 
            var i = 0;
            networkTimestamp.CopyTo(package, i);
            i += TIMESTAMP_SIZE;

            package[i] = type;
            i += TYPE_SIZE;

            channelNameSize.CopyTo(package, i);
            i += SIZE_OF_NAME_SIZE;

            channelName.CopyTo(package, i);
            i += channelName.Length;

            payloadSize.CopyTo(package, i);
            i += SIZE_OF_PAYLOAD_SIZE;

            payload.CopyTo(package, i);

            await _streamOut.WriteAsync(package, 0, package.Length);
            await _streamOut.FlushAsync();
        }
#pragma warning restore AsyncFixer01 // Unnecessary async/await usage

        #endregion

        #region Receiving

        public Package ReadChannel(string channelName)
        {
            var channelQueue = _channelBuffers[channelName];

            Package package;
            if (channelQueue.Count > 0 && channelQueue.TryDequeue(out package))
            {
                return package;
            }

            return EMPTY_PKG;
        }

        private void RunListening()
        {
            while (!_noConnection)
            {
                StoreInput();
            }
        }

        private void StoreInput()
        {
            if (_noConnection)
            {
                return;
            }

            var timestamp = new byte[TIMESTAMP_SIZE];
            var byteCount = _streamIn.Read(timestamp, 0, TIMESTAMP_SIZE);
            if (byteCount != TIMESTAMP_SIZE)
            {
                return;
            }

            var type = new byte[TYPE_SIZE];
            _streamIn.Read(type, 0, TYPE_SIZE);

            var sizeOfNameSize = new byte[SIZE_OF_NAME_SIZE];
            _streamIn.Read(sizeOfNameSize, 0, SIZE_OF_NAME_SIZE);

            if (BitConverter.IsLittleEndian) Array.Reverse(sizeOfNameSize);
            var nameSize = (int) BitConverter.ToUInt32(sizeOfNameSize, 0);
            var pkgName = new byte[nameSize];
            _streamIn.Read(pkgName, 0, nameSize);

            var sizeOfPayloadSize = new byte[SIZE_OF_PAYLOAD_SIZE];
            _streamIn.Read(sizeOfPayloadSize, 0, SIZE_OF_PAYLOAD_SIZE);

            if (BitConverter.IsLittleEndian) Array.Reverse(sizeOfPayloadSize);
            var size = (int) BitConverter.ToUInt32(sizeOfPayloadSize, 0);
            var payload = new byte[size];
            _streamIn.Read(payload, 0, size);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(timestamp);
            }

            var pkg = new Package
            {
                Timestamp = BitConverter.ToDouble(timestamp, 0),
                PkgType = type[0],
                ChannelName = Encoding.ASCII.GetString(pkgName),
                // still in big endian order but in correct array order
                Payload = payload
            };

            _channelBuffers[pkg.ChannelName].Enqueue(pkg);
        }

        private void StopListening()
        {
            if (_listeningTask == null) return;

            _listeningTask.Wait();
            _listeningTask = null;
        }

        private void StopStream()
        {
            if (_noConnection) return;

            _noConnection = true;
            StopListening();
#if NETFX_CORE
            _socket.Dispose();
            _socket = null;
            _streamIn = null;
            _streamOut = null;
#endif
        }

        #endregion

        public void OnDestroy()
        {
            StopStream();
        }
    }
}