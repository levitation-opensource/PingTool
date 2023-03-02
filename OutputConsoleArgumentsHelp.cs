
#region Copyright (c) 2014, Roland Pihlakas
/////////////////////////////////////////////////////////////////////////////////////////
//
// Copyright (c) 2014, Roland Pihlakas.
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
//
/////////////////////////////////////////////////////////////////////////////////////////
#endregion Copyright (c) 2014, Roland Pihlakas


using System;
using System.Diagnostics;

namespace PingTool
{
    partial class Program
    {

        static void OutputConsoleArgumentsHelp()
        {
            Console.WriteLine("\n");
            Console.WriteLine("A program to monitor a number of hosts using ping.");
            Console.WriteLine("The program exits when all monitored hosts have an outage.");
            Console.WriteLine("Program arguments and their default values:");
            Console.WriteLine("\n");

            Console.WriteLine(string.Format("{0} ({1})", ArgShowHelp, "Shows help text"));

            OutputConsoleArgumentHelp(ArgOutageTimeBeforeGiveUpSeconds, ValueOutageTimeBeforeGiveUpSeconds, "How long outage should last before trigger is activated and PingTool quits. NB! This timeout starts only after the failure count specified with " + ArgOutageConditionNumPings + " has been exceeded.");
            OutputConsoleArgumentHelp(ArgOutageConditionNumPings, ValueOutageConditionNumPings, "How many pings should fail before outage can be declared");
            OutputConsoleArgumentHelp(ArgPassedPingIntervalMs, ValuePassedPingIntervalMs, "How many ms to pause after a successful ping");
            OutputConsoleArgumentHelp(ArgFailedPingIntervalMs, ValueFailedPingIntervalMs, "How many ms to pause after a failed ping");
            OutputConsoleArgumentHelp(ArgPingTimeoutMs, ValuePingTimeoutMs, "Ping timeout");
            OutputConsoleArgumentHelp(ArgHost, "host name", "Host name. Multiple host names can be specified. In this case the program exits only after ALL monitored hosts have failed.");
            OutputConsoleArgumentHelp(ArgSourceHost, "source host name", "Source host name. Multiple source host names can be specified. If multiple names are specified then each target host is pinged through corresponding source host (interface). If no source host is specified for some target host then the ping path is choosen by the system.");

            Console.WriteLine("\n");

        }   //static void OutputHelp()

        // ############################################################################

        public static void OutputConsoleArgumentHelp<T>(string name, T defaultValue, string description) 
        {
            Console.WriteLine(string.Format("{0}={1} ({2})", name, defaultValue, description));
        }

        // ############################################################################

    }
}
