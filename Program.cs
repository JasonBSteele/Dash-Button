using System;
using System.Net.NetworkInformation;
using PacketDotNet;
using SharpPcap;

namespace dash_button {
    class Program {
        private const int ReadTimeoutMilliseconds = 1000;

        private static PhysicalAddress dashMac;
        private static DateTime lastEventTime;

        static void Main(string[] args) {
            try {
                dashMac = PhysicalAddress.Parse(Properties.Settings.Default.DashMac.Replace("-", "").Replace(":", "").ToUpper());
            } catch (Exception e) {
                if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.DashMac)) {
                    Console.WriteLine("Error: Could not parse dash mac address: " + e.Message);
                    return;
                }
            }
            
            CaptureDeviceList devices = CaptureDeviceList.Instance;

            if (devices.Count < 1) {
                Console.WriteLine("No devices were found on this machine");
                return;
            }

            Console.WriteLine("The following devices are available on this machine:");
            for (int i = 0; i < devices.Count; i++) {
                ICaptureDevice dev = devices[i];
                Console.WriteLine($"{i}: {dev.Description}");
            }

            ICaptureDevice device = devices[Properties.Settings.Default.InterfaceIndex];
            device.OnPacketArrival += device_OnPacketArrival;
            device.Open(DeviceMode.Promiscuous, ReadTimeoutMilliseconds);
            device.StartCapture();
            Console.WriteLine($"-- Listening on {Properties.Settings.Default.InterfaceIndex}, hit 'Enter' to stop...");
            Console.ReadLine();
            device.StopCapture();
            device.Close();
        }
        
        private static void device_OnPacketArrival(object sender, CaptureEventArgs e) {
            var packet = Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
            while (packet != null) {
                var arpPacket = packet as ARPPacket;
                if (arpPacket != null) {
                    HandleArpPacket(arpPacket);
                }

                packet = packet.PayloadPacket;
            }
        }

        private static void HandleArpPacket(ARPPacket arpPacket) {
            if (Properties.Settings.Default.DiscoveryMode) {
                Console.WriteLine(arpPacket);
            }

            if (arpPacket.SenderHardwareAddress.Equals(dashMac)) {
                Console.WriteLine(DateTime.Now + " Dash ARP");

                // Dash seems to send two ARP packets per button press, one second apart.
                // Dash device is active (blinking) for about 10 seconds after button press,
                // and doesn't allow another press for another 25 seconds.
                // 36 seconds after the initial push, the device sends the same two ARP packets.
                var now = DateTime.Now;
                if (now - Properties.Settings.Default.DuplicateIgnoreInterval > lastEventTime) {
                    lastEventTime = now;
                    Console.WriteLine("Dash button event");

                    // TODO: What to do here?
                }
            }
        }
    }
}
