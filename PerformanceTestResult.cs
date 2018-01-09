/*
	Name:		PerformanceTestResult.cs
	Created:	06.01.2017
	Author:		Viktoria Jechsmayr

*/

namespace SmartHomePerformanceTest
{
    public class PerformanceTestResult
    {
        public PerformanceTestResult(int sentPackets, int receivedPackets)
        {
            SentPackets = sentPackets;
            ReceivedPackets = receivedPackets;
        }

        public int SentPackets { get; }

        public int ReceivedPackets { get; }

        public int LostPackets => SentPackets - ReceivedPackets;

        public double LostPacketsPercent => (LostPackets / (double)SentPackets) * 100;
    }
}
