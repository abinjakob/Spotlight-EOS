using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Yort.Ntp;

namespace Network
{
    public class NtpSync : MonoBehaviour
    {
        [SerializeField] private string _ntpAddress = "pool.ntp.org"; // default NTP server
        [Range(30, 3000)][SerializeField] private int _syncInterval = 60; // Sync interval in seconds
        [SerializeField] private bool _enableNtpSync = true;

        public double NtpTimeOffset { set; get; }

        private void Start()
        {
            print("Sync setup...");
            InvokeRepeating(nameof(CalculateTimeOffset), 0.0f, _syncInterval);
        }

        private async void CalculateTimeOffset()
        {
            if (_enableNtpSync)
            {
                print("Sync...");
                // On UWP or NETFX_CORE
#if UNITY_WSA_10_0 || NETFX_CORE
                var currentTime = await RequestTimeFromNtpServerAsync(_ntpAddress);
                DateTime ntpNow = currentTime;
                NtpTimeOffset = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - ntpNow.Ticks).TotalSeconds;
#else
                // On Windows Desktop or other platforms
                var currentTime = RequestTimeFromNtpServer(_ntpAddress);
                DateTime ntpNow = currentTime;
                NtpTimeOffset = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - ntpNow.Ticks).TotalSeconds;
#endif
            }
        }

#if UNITY_WSA_10_0 || NETFX_CORE
        // For UWP or .NET Core platforms, use NtpClient (already implemented)
        private async System.Threading.Tasks.Task<DateTime> RequestTimeFromNtpServerAsync(string ntpAddress)
        {
            NtpClient ntpClient = new NtpClient(ntpAddress);
            var currentTime = await ntpClient.RequestTimeAsync();
            return currentTime;
        }
#endif

        // For Windows Desktop or other platforms, use standard NTP request via UDP
        private DateTime RequestTimeFromNtpServer(string ntpAddress)
        {
            const byte NtpDataLength = 48; // Length of the NTP data packet
            byte[] ntpData = new byte[NtpDataLength];
            ntpData[0] = 0x1B; // NTP request header byte

            var address = Dns.GetHostEntry(ntpAddress).AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(address, 123); // NTP server port is 123

            using (UdpClient udpClient = new UdpClient())
            {
                udpClient.Connect(endPoint);
                udpClient.Send(ntpData, ntpData.Length);
                ntpData = udpClient.Receive(ref endPoint);
            }

            // Extract time from the response
            ulong intPart = (ulong)ntpData[43] << 8 | ntpData[42];
            intPart = intPart << 8 | ntpData[41];
            intPart = intPart << 8 | ntpData[40];

            ulong fracPart = (ulong)ntpData[47] << 8 | ntpData[46];
            fracPart = fracPart << 8 | ntpData[45];
            fracPart = fracPart << 8 | ntpData[44];

            // Convert NTP time to Unix epoch time (UTC)
            ulong unixTime = intPart - 2208988800UL; // NTP epoch begins 1 January 1900
            double unixFraction = fracPart / (double)0x100000000L;
            double currentTime = unixTime + unixFraction;

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(currentTime);
        }
    }
}
