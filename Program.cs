
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
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Runtime;
using System.Reflection;
using System.Security;

namespace PingTool
{
    // Summary:
    //     Encapsulates a method that takes no parameters and does not return a value.
    public delegate void Action();  //.net 3.0 does not contain Action delegate declaration

    [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
    partial class Program
    {
        [DllImport("kernel32.dll")]
        internal static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [/*DllImport("kernel32.dll")*/DllImport("psapi.dll")]
        internal static extern bool EmptyWorkingSet(IntPtr processHandle);

        static MultiAdvancedPinger multiPinger = null;

        // ############################################################################

        [STAThread]         //prevent message loop creation
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine(Application.ProductName + " / Version: " + Application.ProductVersion);
            Console.WriteLine();


            GetConsoleArgumentsValues(args);


            if (ValueShowHelp)
            {
                OutputConsoleArgumentsHelp();
                return;
            }


            ExitHandler.InitUnhandledExceptionHandler();
            ExitHandler.HookSessionEnding();
            ExitHandler.ExitEventOnce += ExitEventHandler;


            Process CurrentProcess = Process.GetCurrentProcess();
            IntPtr handle = CurrentProcess.Handle;
            CurrentProcess.PriorityClass = ProcessPriorityClass.High;
            NativeMethods.SetPagePriority(handle, 1);   //NB! lowest Page priority
            NativeMethods.SetIOPriority(handle, NativeMethods.PROCESSIOPRIORITY.PROCESSIOPRIORITY_NORMAL);   //ensure that we do not inherit low IO priority from the parent process or something like that


            GC.Collect(2, GCCollectionMode.Forced);     //collect now all unused startup info because later we will be relatively steady state and will not need any more much memory management
            GCSettings.LatencyMode = GCLatencyMode.Batch; //most intrusive mode - most efficient   //This option affects only garbage collections in generation 2; generations 0 and 1 are always non-concurrent because they finish very fast.  - http://msdn.microsoft.com/en-us/library/ee787088(v=VS.110).aspx#workstation_and_server_garbage_collection
            try
            {
                //GC.WaitForFullGCComplete();   //cob roland: the exception cannot be caught when the method name is written inline, see also http://stackoverflow.com/questions/3546580/why-is-it-not-possible-to-catch-missingmethodexception
                typeof(GC).InvokeMember("WaitForFullGCComplete", 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, 
                    null, null, null);    //see ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/fxref_mscorlib/html/9926d3b0-b0ef-e965-bc72-9ee34bf84df5.htm
            }
            catch (MissingMethodException)  //GC.WaitForFullGCComplete() is only available on .NET SP1 versions
            {
                Thread.Sleep(100);
            }


            AutoResetEvent GC_WaitForPendingFinalizers_done = new AutoResetEvent(false);
            Thread thread = new Thread(() =>
            {
                // Wait for all finalizers to complete before continuing.
                // Without this call to GC.WaitForPendingFinalizers, 
                // the worker loop below might execute at the same time 
                // as the finalizers.
                // With this call, the worker loop executes only after
                // all finalizers have been called.
                GC.WaitForPendingFinalizers();

                GC_WaitForPendingFinalizers_done.Set();
            });
            thread.Name = "GC.WaitForPendingFinalizers thread";
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;    //Background threads are identical to foreground threads, except that background threads do not prevent a process from terminating.
            thread.Start();

            GC_WaitForPendingFinalizers_done.WaitOne(10000);    //NB! prevent hangs



            NativeMethods.SetProcessWorkingSetSize(handle, new IntPtr(-1), new IntPtr(-1));   //empty the working set
            NativeMethods.EmptyWorkingSet(handle);   //empty the working set


            try  
            {
                multiPinger = new MultiAdvancedPinger(ValueHosts, ValueSourceHosts);
                {
                    multiPinger.PingUntilOutageOrCancel
                    (
                        ValueOutageTimeBeforeGiveUpSeconds,
                        ValueOutageConditionNumPings,
                        ValuePassedPingIntervalMs,
                        ValueFailedPingIntervalMs,
                        ValuePingTimeoutMs,
                        /*pingSuccessCallback = */null
                    );
                }
            }
            finally 
            {
                if (multiPinger != null)
                    multiPinger.Dispose();
                multiPinger = null;
            }

            //exit the program...
        }

        // ############################################################################

        static void ExitEventHandler(bool hasShutDownStarted)
        {
            if (multiPinger != null)
                multiPinger.SetCancelFlag();

            Console.WriteLine("Exiting...");
            ExitHandler.DoExit();
        }
    }   //partial class Program

    // ############################################################################

    public class MultiAdvancedPinger : IDisposable 
    {
        internal static IntPtr CurrentProcessHandle;

        List<AdvancedPinger> advancedPingers = new List<AdvancedPinger>();
        List<Thread> advancedPingerThreads = new List<Thread>();

        volatile bool cancelFlag = false;
        volatile int currentOutageHostCount = 0;

        // ############################################################################

        public MultiAdvancedPinger(IEnumerable<string> hosts)
        {
            foreach (string host in hosts)
                advancedPingers.Add(new AdvancedPinger(host));
        }

        public MultiAdvancedPinger(IEnumerable<string> hosts, IEnumerable<string> sourceHosts)
        {
            using (IEnumerator<string> sourceHostEnumerator = sourceHosts.GetEnumerator())
            {
                foreach (string host in hosts)
                {
                    string sourceHost;
                    if (sourceHostEnumerator.MoveNext())
                        sourceHost = sourceHostEnumerator.Current;
                    else
                        sourceHost = null;

                    advancedPingers.Add(new AdvancedPinger(host, sourceHost));
                }
            }
        }

        // ############################################################################

        public void SetCancelFlag()
        {
            this.cancelFlag = true;

            foreach (var advancedPinger in advancedPingers)     //propagate cancel flag to all pingers
                advancedPinger.SetCancelFlag();
        }

        // ############################################################################

        public void Dispose()
        {
            if (advancedPingers != null)
            {
                foreach (var advancedPinger in advancedPingers)
                    advancedPinger.Dispose();

                advancedPingers = null;
            }
        }

        // ############################################################################

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outageTimeBeforeGiveUpSeconds"></param>
        /// <param name="outageConditionNumPings"></param>
        /// <param name="passedPingIntervalMs"></param>
        /// <param name="failedPingIntervalMs"></param>
        /// <param name="timeoutMs"></param>
        /// <param name="pingSuccessCallback">NB! pingSuccessCallback should be <b>threadsafe</b> since it can be called from multiple threads simultaneously</param>
        /// <returns></returns>
        public bool PingUntilOutageOrCancel(int outageTimeBeforeGiveUpSeconds, int outageConditionNumPings, int passedPingIntervalMs, int failedPingIntervalMs, int timeoutMs, Action pingSuccessCallback)
        {
            currentOutageHostCount = 0;

            //start all parallel pinger threads
            foreach (var advancedPinger1 in advancedPingers)
            {
                AdvancedPinger advancedPinger = advancedPinger1;    //NB! need to copy so that each thread has separate pinger variable instance

                Thread thread = new Thread(t => 
                {
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;


                    bool outageBegun = false;
                    do
                    {
                        bool success = advancedPinger.PingUntilOutageOrCancel
                        (
                            outageBegun,
                            outageTimeBeforeGiveUpSeconds,
                            outageConditionNumPings,
                            passedPingIntervalMs,
                            failedPingIntervalMs,
                            timeoutMs,
                            () =>
                            {
                                if (pingSuccessCallback != null)
                                    pingSuccessCallback();

                                Debug.Assert(currentOutageHostCount > 0);
#pragma warning disable 0420    //warning CS0420: 'PingTool.MultiAdvancedPinger.currentOutageHostCount': a reference to a volatile field will not be treated as volatile
                                Interlocked.Decrement(ref currentOutageHostCount);
#pragma warning restore 0420
                                Debug.Assert(currentOutageHostCount >= 0);

                                outageBegun = false; //NB!
                            }
                        );

                        if (this.cancelFlag)
                            return;
                        Debug.Assert(!success);


                        if (!outageBegun)     //NB! count each pinger's outage only once per outage begin
                        {
#pragma warning disable 0420    //warning CS0420: 'PingTool.MultiAdvancedPinger.currentOutageHostCount': a reference to a volatile field will not be treated as volatile
                            Interlocked.Increment(ref currentOutageHostCount);
#pragma warning restore 0420
                            outageBegun = true;
                        }

                        //check whether there is a global outage occurring
                        if (currentOutageHostCount == advancedPingers.Count)
                        {
                            foreach (var otherPinger in advancedPingers)     //propagate cancel flag to all pingers but do not set cancel flag in current MultiPinger object. Actually this should not be necessary since all pingers should be exiting anyway
                                otherPinger.SetCancelFlag();

                            return;     //now here we quit from the loop
                        }   //if (currentOutageHostCount == advancedPingers.Count)
                    }
                    while (true);   //NB! repeat the pinger even when it encountered an outage
                });
                thread.Start();

                advancedPingerThreads.Add(thread);
            
            }   //foreach (var advancedPinger in advancedPingers)


            Thread.CurrentThread.Priority = ThreadPriority.Lowest;



            GC.Collect(2, GCCollectionMode.Forced);     //collect now all unused startup info because later we will be relatively steady state and will not need any more much memory management

            GCSettings.LatencyMode = GCLatencyMode.Batch; //most intrusive mode - most efficient   //This option affects only garbage collections in generation 2; generations 0 and 1 are always non-concurrent because they finish very fast.  - http://msdn.microsoft.com/en-us/library/ee787088(v=VS.110).aspx#workstation_and_server_garbage_collection
            GC.WaitForFullGCComplete();

            CurrentProcessHandle = Process.GetCurrentProcess().Handle;
            TrimWorkingSet(CurrentProcessHandle);



            //sit here until all pinger threads have exited
            foreach (var thread in advancedPingerThreads)
            {
                thread.Join();
            }

            return this.cancelFlag;
        }

        internal static void TrimWorkingSet(IntPtr handle)
        {
            try
            {
                Program.SetProcessWorkingSetSize(handle, new IntPtr(-1), new IntPtr(-1));   //empty the working set
            }
            catch
            {
            }

            try
            {
                Program.EmptyWorkingSet(handle);   //empty the working set
            }
            catch
            {
            }
        }

    }   //class MultiAdvancedPinger 

    // ############################################################################

    public class AdvancedPinger : BasicPinger
    {
        volatile bool cancelFlag = false;

        // ############################################################################

        public AdvancedPinger(string host)
            : base(host)
        {
        }

        public AdvancedPinger(string host, int timeoutMs_in)
            : base(host, timeoutMs_in)
        {
        }

        public AdvancedPinger(string host, string sourceHost)
            : base(host, sourceHost)
        {
        }

        public AdvancedPinger(string host, string sourceHost, int timeoutMs_in)
            : base(host, sourceHost, timeoutMs_in)
        {
        }

        // ############################################################################

        public void SetCancelFlag()
        {
            this.cancelFlag = true;
        }

        // ############################################################################

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outageTimeBeforeGiveUpSeconds"></param>
        /// <param name="outageConditionNumPings"></param>
        /// <param name="passedPingIntervalMs"></param>
        /// <param name="failedPingIntervalMs"></param>
        /// <param name="timeoutMs"></param>
        /// <param name="pingSuccessCallback">NB! The callback is called only <b>once</b> after each outage end</param>
        /// <returns></returns>
        public bool PingUntilOutageOrCancel(bool outer_outageState, int outageTimeBeforeGiveUpSeconds, int outageConditionNumPings, int passedPingIntervalMs, int failedPingIntervalMs, int timeoutMs, Action pingSuccessCallback)
        {
            DateTime? outageBegin = null;
            do
            {
                bool success = PingUntilOutageOrCancel
                (
                    outageBegin != null,
                    outageConditionNumPings, 
                    passedPingIntervalMs, 
                    failedPingIntervalMs, 
                    timeoutMs,
                    () => 
                    {
                        if (outer_outageState)    //NB! propagate the success message only when the outage was started
                        {
                            if (pingSuccessCallback != null)
                                pingSuccessCallback();

                            outer_outageState = false;
                        }

                        outageBegin = null;     //reset outage status
                    }
                );

                if (this.cancelFlag)
                    break;
                Debug.Assert(!success);

                DateTime now = DateTime.UtcNow;
                if (outageBegin == null)
                {
                    outageBegin = now;
                }
                else
                {
                    TimeSpan outageDuration = now - outageBegin.Value;

                    if (outageDuration.TotalSeconds >= outageTimeBeforeGiveUpSeconds)    //should we give up?
                        break;
                }
            }
            while (true);

            return this.cancelFlag;
        }

        // ############################################################################

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outageConditionNumPings"></param>
        /// <param name="passedPingIntervalMs"></param>
        /// <param name="failedPingIntervalMs"></param>
        /// <param name="timeoutMs"></param>
        /// <param name="pingSuccessCallback">NB! The callback is called only <b>once</b> after each outage end</param>
        /// <returns></returns>
        public bool PingUntilOutageOrCancel(bool outer_outageState, int outageConditionNumPings, int passedPingIntervalMs, int failedPingIntervalMs, int timeoutMs, Action pingSuccessCallback)
        {
            this.cancelFlag = false;

            int outageCount = 0;
            bool success;
            do
            {
                success = base.PingHost(timeoutMs);

                if (success)
                {
                    if (outer_outageState)    //NB! propagate the success message only when the outage was started
                    {
                        if (pingSuccessCallback != null)
                            pingSuccessCallback();

                        outer_outageState = false;
                    }
                    
                    outageCount = Math.Max(0, outageCount - 1);

                    if (!this.cancelFlag)
                    {
                        if (outageCount == 0)
                        {
                            //Thread.Sleep(passedPingIntervalMs);
                            int sleepStep = 1000;
                            for (int i = 0; i < passedPingIntervalMs; i += sleepStep)
                            {
                                if (!this.cancelFlag)
                                    Thread.Sleep(Math.Min(sleepStep, passedPingIntervalMs - i));
                            }
                        }
                        else    //if (outageCount == 0)
                        {
                            //Thread.Sleep(failedPingIntervalMs);
                            int sleepStep = 1000;
                            for (int i = 0; i < failedPingIntervalMs; i += sleepStep)
                            {
                                if (!this.cancelFlag)
                                    Thread.Sleep(Math.Min(sleepStep, failedPingIntervalMs - i));
                            }
                        }
                    }   //if (!this.cancelFlag)  
                }
                else    //if (success)
                {
                    outageCount++;
                    if (!this.cancelFlag 
                        //&& outageCount < outageConditionNumPings)   //sleep only when outage count not exceeded   //cob roland: sleep also when outage count is exceeded since we are likely going to repeat the loop
                    )
                    {
                        //Thread.Sleep(failedPingIntervalMs);
                        int sleepStep = 1000;
                        for (int i = 0; i < failedPingIntervalMs; i += sleepStep)
                        {
                            if (!this.cancelFlag)
                                Thread.Sleep(Math.Min(sleepStep, failedPingIntervalMs - i));
                        }
                    }
                }    //if (success)

                MultiAdvancedPinger.TrimWorkingSet(MultiAdvancedPinger.CurrentProcessHandle);
            }
            while (outageCount < outageConditionNumPings && !this.cancelFlag);

            return this.cancelFlag;
        }

    }   //class AdvancedPinger

    // ############################################################################

    [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
    public class BasicPinger : IDisposable
    {
        IPAddressEx address;
        IPAddressEx sourceAddress;
        PingOptionsEx pingOptions;
        PingEx ping;
        int bufferLength;  
        Random rand;
        int timeoutMs;
        const int defaultTimeoutMs = 5000;  //.NET default ping timeout

        // ############################################################################

        public BasicPinger(string host)
            : this(host, null, defaultTimeoutMs)
        {

        }

        public BasicPinger(string host, string sourceHost)
            : this(host, sourceHost, defaultTimeoutMs)
        {

        }

        public BasicPinger(string host, int timeoutMs_in)
            : this(host, null, timeoutMs_in)
        {

        }

        public BasicPinger(string host, string sourceHost, int timeoutMs_in)
        {
            //IPAddress instance for holding the returned host
            this.address = GetIpFromHost(host);
            this.sourceAddress = GetIpFromHost(sourceHost);

            //set the ping options, TTL 128
            this.pingOptions = new PingOptionsEx(128, /*dontFragment = */false);     //TODO: dontFragment?

            this.bufferLength = 32;     

            this.rand = new Random(unchecked((int)DateTime.UtcNow.Ticks));

            //create a new ping instance
            this.ping = new PingEx();            

            this.timeoutMs = timeoutMs_in;


            string destStr;
            if (address.ToString() != host)
                destStr = string.Format("{0} [{1}]", host, address);
            else
                destStr = host;

            string sourceStr;
            if (sourceHost != null && sourceAddress.ToString() != sourceHost)
                sourceStr = string.Format("{0} [{1}]", sourceHost, sourceAddress);
            else
                sourceStr = sourceHost;


            if (sourceStr != null)
                Console.WriteLine("Pinging {0} from {1} with {2} bytes of data:", destStr, sourceStr, bufferLength);
            else
                Console.WriteLine("Pinging {0} with {1} bytes of data:", destStr, bufferLength);

        }   //public BasicPinger(string host, string sourceHost, int timeoutMs_in)

        // ############################################################################

        public void Dispose()
        {
            if (ping != null)
            {
                ping.Dispose();
                ping = null;
            }
        }

        // ############################################################################

        public bool PingHost()
        {
            string returnMessage;
            bool success = PingHost(out returnMessage);
            Console.WriteLine("ping {0}: {1}", this.address, returnMessage);
            return success;
        }

        public bool PingHost(int timeoutMs_in)
        {
            string returnMessage;
            bool success = PingHost(out returnMessage, timeoutMs_in);
            Console.WriteLine("ping {0}: {1}", this.address, returnMessage);
            return success;
        }

        public bool PingHost(out string returnMessage)
        {
            return PingHost(out returnMessage, this.timeoutMs);
        }

        public bool PingHost(out string returnMessage, int timeoutMs_in)
        {
            //first make sure we actually have an internet connection
            if (HasConnection())
            {
                try
                {
                    //32 byte buffer (create empty)
                    byte[] buffer = new byte[bufferLength];

                    //the program should use also a random buffer (that changes at every ping) so it gives more realistic results under a network with realtime compression that is used typically with Dial-Up connections. 
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        buffer[i] = (byte)rand.Next(256);
                    }


                    //send the ping 4 times to the host and record the returned data.
                    //The Send() method expects 4 items:
                    //1) The IPAddress we are pinging
                    //2) The timeout value
                    //3) A buffer (our byte array)
                    //4) PingOptions
                    PingReplyEx pingReply = ping.Send(sourceAddress, address, timeoutMs_in, buffer, pingOptions);

                    //make sure we dont have a null reply
                    if (!(pingReply == null))
                    {
                        switch (pingReply.Status)
                        {
                            case IPStatus.Success:

                                bool rval = true;

                                StringBuilder returnMessage_sb = new StringBuilder();

                                int miscompare_at = -1;
                                int compareLen = Math.Min(buffer.Length, pingReply.Buffer.Length);
                                for (int i = 0; i < compareLen; i++)
                                {
                                    if (pingReply.Buffer[i] != buffer[i])
                                    {
                                        miscompare_at = i;
                                        break;
                                    }
                                }


                                returnMessage_sb.AppendFormat("Reply from {0}: bytes={1}", pingReply.Address, pingReply.Buffer.Length);
                                if (pingReply.Buffer.Length < buffer.Length)
                                {
                                    rval = false;
                                    returnMessage_sb.AppendFormat(" (sent {0})", buffer.Length);
                                }
                                if (miscompare_at != -1)
                                {
                                    rval = false;
                                    returnMessage_sb.AppendFormat(" - MISCOMPARE at offset {0} -", miscompare_at);
                                }
                                returnMessage_sb.AppendFormat(" time={0}ms TTL={1}", pingReply.RoundtripTime, pingReply.Options.Ttl);

                                if (sourceAddress != null)
                                    returnMessage_sb.AppendFormat(" via {0}", sourceAddress);

                                returnMessage = returnMessage_sb.ToString();

                                return rval;
                                //break;

                            case IPStatus.TimedOut:
                                returnMessage = "Request timed out.";
                                break;

                            default:
                                if (pingReply.Status.ToString() == "DestinationNetworkUnreachable")
                                    returnMessage = "Destination net unreachable.";
                                else
                                    returnMessage = string.Format("PING: transmit failed. General failure. Status: {0}", pingReply.Status.ToString());
                                break;
                        }
                    }
                    else
                    {
                        returnMessage = "PING: transmit failed. General failure. Connection failed for an unknown reason...";
                    }
                }
                catch (PingException ex)
                {
                    returnMessage = string.Format("PING: transmit failed. General failure. Connection Error: {0}", ex.Message);
                }
                catch (SocketException ex)
                {
                    returnMessage = string.Format("PING: transmit failed. General failure. Socket Error: {0}", ex.Message);
                }
            }
            else   //if (HasConnection())
            {
                returnMessage = "PING: transmit failed. General failure. No Internet connection found...";
            }

            return false;

        }   //public bool PingHost(out string returnMessage)

        // ############################################################################

        /// <summary>
        /// method for retrieving the IP address from the host provided
        /// </summary>
        /// <param name="host">the host we need the address for</param>
        /// <returns></returns>
        private static IPAddressEx GetIpFromHost(string host)
        {
            if (host == null)
                return null;

            //variable to hold our error message (if something fails)
            //string errMessage = string.Empty;

            //IPAddress instance for holding the returned host
            IPAddressEx address = null;

            //wrap the attempt in a try..catch to capture
            //any exceptions that may occur
            try
            {
                //get the host IP from the name provided                
                if (!IPAddressEx.TryParse(host, out address))     //first try to parse as IP string if this fails only then try DNS name resolving
                {
                    address = new IPAddressEx(Dns.GetHostEntry(host).AddressList[0].GetAddressBytes());
                }
            }
            catch (SocketException ex)
            {
                //some DNS error happened, return the message
                string errMessage = string.Format("host: {0} DNS Error: {1}", host, ex.Message);
                Console.WriteLine(errMessage);

                ExitHandler.DoExit();     //NB!
                //address = null;
            }
            return address;
        }

        // ############################################################################

        /// <summary>
        /// enum to hold the possible connection states
        /// </summary>
        [Flags]
        enum ConnectionStatusEnum : int
        {
            INTERNET_CONNECTION_MODEM = 0x1,
            INTERNET_CONNECTION_LAN = 0x2,
            INTERNET_CONNECTION_PROXY = 0x4,
            INTERNET_RAS_INSTALLED = 0x10,
            INTERNET_CONNECTION_OFFLINE = 0x20,
            INTERNET_CONNECTION_CONFIGURED = 0x40
        }

        [DllImport("wininet", CharSet = CharSet.Auto)]
        static extern bool InternetGetConnectedState(ref ConnectionStatusEnum flags, int dw);

        /// <summary>
        /// method to check the status of the pinging machines internet connection
        /// </summary>
        /// <returns></returns>
        private static bool HasConnection()
        {
            //instance of our ConnectionStatusEnum
            ConnectionStatusEnum state = 0;

            //call the API
            InternetGetConnectedState(ref state, 0);

            //check the status, if not offline and the returned state
            //isnt 0 then we have a connection
            if (((int)ConnectionStatusEnum.INTERNET_CONNECTION_OFFLINE & (int)state) != 0)
            {
                //return true, we have a connection
                return false;
            }
            //return false, no connection available
            return true;
        }

        // ############################################################################


    }   //class BasicPinger
}
