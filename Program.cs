/*
	Name:		Program.cs
	Created:	06.01.2017
	Author:		Viktoria Jechsmayr

*/


using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SmartHomePerformanceTest
{
    class Program
    {
        private const int SMARTHOME_PORT = 8888;

        private const byte TARGET_SMARTHOME_ID = 2;
        private const byte TARGET_SUBDEVICE_ID = 1;

        private const byte COMMAND_GETVALUE_REQUEST_ID = 5;
        private const byte COMMAND_GETVALUE_REPLY_ID = 6;

        private static readonly IPEndPoint TargetEndPoint = new IPEndPoint(IPAddress.Broadcast, SMARTHOME_PORT);

        /*
         * Provides a Menu to choose Automatic or Manual Test
         */
        static void Main(string[] args)
        {
            Console.Write("Automatischer Test? [j/n]: ");
            var key = Console.ReadKey().KeyChar;

            Console.WriteLine();
            Console.WriteLine();

            if (key == 'j')
            {
                DoAutomaticTest().Wait();
            }
            else
            {
                DoManualTest().Wait();
            }

            Console.ReadLine();
        }

        /*
         * Automatic Test runns the Test over defined duration up to number of possible responses of Arduino
         */
        private static async Task DoAutomaticTest()
        {
            int packetsPerSecond = 1;
            int duration = 0;

            while (duration <= 0)
            {
                Console.Write("Dauer pro Test (in Sekunden): ");
                int.TryParse(Console.ReadLine(), out duration);

                if (duration <= 0)
                {
                    Console.WriteLine("Ungültige Eingabe!");
                }
            }

            PerformanceTestResult performanceTestResult;

            Console.WriteLine();
            Console.WriteLine("Automatischer Test läuft...");
            Console.WriteLine();

            var currentTestStep = AutomaticTestStep.DoublePackets;
            byte smartHomeId = 100;

            while (true)
            {
                switch (currentTestStep)
                {
                    case AutomaticTestStep.DoublePackets:
                        packetsPerSecond = packetsPerSecond * 2;
                        break;
                    case AutomaticTestStep.AddOnePacket:
                        packetsPerSecond++;
                        break;
                }

                performanceTestResult = await DoPerformanceTest(packetsPerSecond, duration, smartHomeId);

                Console.Write($"Verlust bei {packetsPerSecond} Paketen pro Sekunde: {performanceTestResult.LostPackets} Pakete ");
                Console.Write($"({performanceTestResult.LostPacketsPercent:0.##}%) ");
                Console.WriteLine($"(Gesendet: {performanceTestResult.SentPackets} Pakete) ");

                if (performanceTestResult.LostPackets > 0)
                {
                    if (currentTestStep == AutomaticTestStep.DoublePackets)
                    {
                        await Task.Delay(5000);
                        packetsPerSecond = packetsPerSecond - packetsPerSecond / 4;

                        currentTestStep = AutomaticTestStep.LowerPacketCountUnitNoLoss;
                    }
                    else if (currentTestStep == AutomaticTestStep.LowerPacketCountUnitNoLoss)
                    {
                        await Task.Delay(5000);
                        packetsPerSecond = packetsPerSecond - packetsPerSecond / 4;
                    }
                    else if (currentTestStep == AutomaticTestStep.AddOnePacket)
                    {
                        break;
                    }
                }
                else if (currentTestStep == AutomaticTestStep.LowerPacketCountUnitNoLoss)
                {
                    currentTestStep = AutomaticTestStep.AddOnePacket;
                }

                smartHomeId++;
                if (smartHomeId > 254)
                {
                    smartHomeId = 100;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Automatischer Test beendet");
        }

        /*
         * Runs Test manual defin duration and Packages per Second to test one specific Case
         */
        private static async Task DoManualTest()
        {
            int packetsPerSecond = 0;
            int duration = 0;

            while (packetsPerSecond <= 0 || packetsPerSecond > 1000 || duration <= 0)
            {
                Console.Write("Pakete pro Sekunde (1 bis 1000): ");
                int.TryParse(Console.ReadLine(), out packetsPerSecond);

                if (packetsPerSecond <= 0 || packetsPerSecond > 1000)
                {
                    Console.WriteLine("Ungültige Eingabe!");
                }
                else
                {
                    Console.Write("Dauer (in Sekunden): ");
                    int.TryParse(Console.ReadLine(), out duration);

                    if (duration <= 0)
                    {
                        Console.WriteLine("Ungültige Eingabe!");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Test läuft...");

            var performanceTestResult = await DoPerformanceTest(packetsPerSecond, duration, 254);

            Console.WriteLine();
            Console.WriteLine($"Gesendet: {performanceTestResult.SentPackets} Pakete");
            Console.WriteLine($"Empfangen: {performanceTestResult.ReceivedPackets} Pakete");
            Console.WriteLine($"Verlust: {performanceTestResult.LostPackets} Pakete ({performanceTestResult.LostPacketsPercent:0.##}%)");
        }

        /*
         * Test the function and checks if sendet and receivedPackages are ident, calculates lost Packages
         */
        private static async Task<PerformanceTestResult> DoPerformanceTest(int packetsPerSecond, int duration, byte smarthomeId)
        {
            var udpClient = new UdpClient(SMARTHOME_PORT);
            udpClient.EnableBroadcast = true;

            var receivedPacketsTask = CountReceivedPacketsAsync(udpClient, smarthomeId);
            var sendPacketsTask = SendPacketsAsync(udpClient, packetsPerSecond, duration, smarthomeId);

            var sentPackets = await sendPacketsTask;

            await Task.Delay(1000);

            udpClient.Close();

            var receivedPackets = await receivedPacketsTask;

            var lostPacketsCount = sentPackets - receivedPackets;
            var lostPacketsPercent = (lostPacketsCount / (double)sentPackets) * 100;

            return new PerformanceTestResult(sentPackets, receivedPackets);
        }

        private static async Task<int> CountReceivedPacketsAsync(UdpClient udpClient, byte smarthomeId)
        {
            var receivedPackets = 0;

            while (true)
            {
                UdpReceiveResult receiveResult;

                try
                {
                    receiveResult = await udpClient.ReceiveAsync();
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
                {
                    break;
                }

                if (receiveResult.Buffer.Length >= 3)
                {
                    var senderId = receiveResult.Buffer[0];
                    var receiverId = receiveResult.Buffer[1];
                    var commandId = receiveResult.Buffer[2];

                    if (senderId == TARGET_SMARTHOME_ID && receiverId == smarthomeId && commandId == COMMAND_GETVALUE_REPLY_ID)
                    {
                        receivedPackets++;
                    }
                }
            }

            return receivedPackets;
        }

        private static async Task<int> SendPacketsAsync(UdpClient udpClient, int packetsPerSecond, int duration, byte smarthomeId)
        {
            var waitTime = 1000 / packetsPerSecond;
            var packetsToSend = packetsPerSecond * duration;
            var sentPackets = 0;

            var bufferToSend = new byte[] { smarthomeId, TARGET_SMARTHOME_ID, COMMAND_GETVALUE_REQUEST_ID, TARGET_SUBDEVICE_ID };

            while (sentPackets < packetsToSend)
            {
                await udpClient.SendAsync(bufferToSend, bufferToSend.Length, TargetEndPoint);
                sentPackets++;

                await Task.Delay(waitTime);
            }

            return sentPackets;
        }
    }
}