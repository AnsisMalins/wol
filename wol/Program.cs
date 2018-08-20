using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace wol
{
    internal static class Program
    {
        private static IPEndPoint Broadcast = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 7);
        private static IPEndPoint BroadcastV6 = new IPEndPoint(IPAddress.Parse("ff02::1"), 7);

        private static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: wol <MAC address>");
                return;
            }

            var match = Regex.Match(args[0],
                @"([0-9a-f]{2})\W?([0-9a-f]{2})\W?([0-9a-f]{2})\W?([0-9a-f]{2})\W?([0-9a-f]{2})\W?([0-9a-f]{2})",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                Console.Error.WriteLine("Not a valid MAC address");
                return;
            }

            byte[] macAddress = new byte[6];
            for (int i = 0; i < 6; i++)
                macAddress[i] = byte.Parse(match.Groups[i + 1].Value, NumberStyles.HexNumber);

            byte[] magicPacket = new byte[102];
            using (var stream = new MemoryStream(magicPacket))
            {
                for (int i = 0; i < 6; i++)
                    stream.WriteByte(0xff);
                for (int i = 0; i < 16; i++)
                    stream.Write(macAddress, 0, macAddress.Length);
            }

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                foreach (var addressInfo in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    IPEndPoint destination;
                    if (addressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                        destination = Broadcast;
                    else if (addressInfo.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        destination = BroadcastV6;
                    else
                        continue;

                    UdpClient client = null;
                    try
                    {
                        client = new UdpClient(new IPEndPoint(addressInfo.Address, 0));
                        client.Send(magicPacket, magicPacket.Length, destination);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"{ex.Message} when sending from {addressInfo.Address}");
                    }
                    finally
                    {
                        if (client != null)
                            client.Dispose();
                    }
                }
            }
        }
    }
}