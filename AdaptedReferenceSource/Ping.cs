//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;

namespace System.Net.NetworkInformation
{
    public delegate void PingCompletedEventHandlerEx(object sender, PingCompletedEventArgsEx e);

    public class PingCompletedEventArgsEx : AsyncCompletedEventArgs
    {
        PingReplyEx reply;

        internal PingCompletedEventArgsEx(PingReplyEx reply, Exception error, bool cancelled, object userToken) : base(error, cancelled, userToken)
        {
            this.reply = reply;
        }
        public PingReplyEx Reply { get { return reply; } }
    }

    public class PingEx : Component
    {
        const int MaxUdpPacket = 0xFFFF + 256; // Marshal.SizeOf(typeof(Icmp6EchoReply)) * 2 + ip header info;  
        const int MaxBufferSize = 65500; //artificial constraint due to win32 api limitations.
        const int DefaultTimeout = 5000; //5 seconds same as ping.exe
        const int DefaultSendBufferSize = 32;  //same as ping.exe

        bool ipv6 = false;
        bool disposeRequested = false;
        object lockObject = new object();

        //used for icmpsendecho apis
        internal ManualResetEvent pingEvent = null;
        private RegisteredWaitHandle registeredWait = null;
        SafeLocalFreeEx requestBuffer = null;
        SafeLocalFreeEx replyBuffer = null;
        int sendSize = 0;  //needed to determine what reply size is for ipv6 in callback

        SafeCloseIcmpHandle handlePingV4 = null;
        SafeCloseIcmpHandle handlePingV6 = null;

        const int ReplyBufferSize = MaxUdpPacket;   //roland

        //new async event support
        SendOrPostCallback onPingCompletedDelegate;
        public event PingCompletedEventHandlerEx PingCompleted;

        // For blocking in SendAsyncCancel()
        ManualResetEvent asyncFinished = null;
        bool InAsyncCall
        {
            get
            {
                if (asyncFinished == null)
                    return false;
                // Never blocks, just checks if a thread would block.
                return !asyncFinished.WaitOne(0);
            }
            set
            {
                if (asyncFinished == null)
                    asyncFinished = new ManualResetEvent(!value);
                else if (value)
                    asyncFinished.Reset(); // Block
                else
                    asyncFinished.Set(); // Clear
            }
        }

        // Thread safety
        private const int Free = 0;
        private const int InProgress = 1;
        private new const int Disposed = 2;
        private int status = Free;

        private void CheckStart(bool async)
        {
            if (disposeRequested)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            int currentStatus = Interlocked.CompareExchange(ref status, InProgress, Free);
            if (currentStatus == InProgress)
            {
                throw new InvalidOperationException("In async");
            }
            else if (currentStatus == Disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (async)
            {
                InAsyncCall = true;
            }
        }

        private void Finish(bool async)
        {
            Debug.Assert(status == InProgress, "Invalid status: " + status);
            status = Free;
            if (async)
            {
                InAsyncCall = false;
            }
            if (disposeRequested)
            {
                InternalDispose();
            }
        }

        protected void OnPingCompleted(PingCompletedEventArgsEx e)
        {
            if (PingCompleted != null)
            {
                PingCompleted(this, e);
            }
        }

        void PingCompletedWaitCallback(object operationState)
        {
            OnPingCompleted((PingCompletedEventArgsEx)operationState);
        }

        public PingEx()
        {
            onPingCompletedDelegate = new SendOrPostCallback(PingCompletedWaitCallback);
        }

        //cancel pending async requests, close the handles
        private void InternalDispose()
        {
            disposeRequested = true;

            if (Interlocked.CompareExchange(ref status, Disposed, Free) != Free)
            {
                // Already disposed, or Finish will call Dispose again once Free
                return;
            }

            if (handlePingV4 != null)
            {
                handlePingV4.Close();
                handlePingV4 = null;
            }

            if (handlePingV6 != null)
            {
                handlePingV6.Close();
                handlePingV6 = null;
            }

            UnregisterWaitHandle();

            if (pingEvent != null)
            {
                pingEvent.Close();
                pingEvent = null;
            }

            if (replyBuffer != null)
            {
                replyBuffer.Close();
                replyBuffer = null;
            }

            if (asyncFinished != null)
            {
                asyncFinished.Close();
                asyncFinished = null;
            }
        }

        private void UnregisterWaitHandle()
        {
            lock (lockObject)
            {
                if (registeredWait != null)
                {
                    registeredWait.Unregister(null);
                    // If Unregister returns false, it is sufficient to nullify registeredWait
                    // and let its own finilizer clean up later.
                    registeredWait = null;
                }
            }
        }

        protected override void Dispose(Boolean disposing)
        {
            if (disposing)
            { // Only on explicit dispose.  Otherwise, the GC can cleanup everything else.
                InternalDispose();
            }
            base.Dispose(disposing);
        }

        public PingReplyEx Send(
            IPAddressEx sourceAddress,    //roland 
            IPAddressEx address, int timeout, byte[] buffer, PingOptionsEx options)
        {
            IPAddressEx sourceAddress2;   //roland
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (buffer.Length > MaxBufferSize)
            {
                throw new ArgumentException("PingEx.Send: Invalid ping buffer size", "buffer");
            }

            if (timeout < 0)
            {
                throw new ArgumentOutOfRangeException("timeout");
            }

            if (address == null)
            {
                throw new ArgumentNullException("address");
            }

            TestIsIpSupported(address); // Address family is installed?

            if (address.Equals(IPAddressEx.Any) || address.Equals(IPAddressEx.IPv6Any))
            {
                throw new ArgumentException("PingEx.Send: Invalid IP address", "address");
            }

            //
            // FxCop: need to snapshot the address here, so we're sure that it's not changed between the permission
            // and the operation, and to be sure that IPAddressEx.ToString() is called and not some override that
            // always returns "localhost" or something.
            //
            IPAddressEx addressSnapshot;
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                addressSnapshot = new IPAddressEx(address.GetAddressBytes());
            }
            else
            {
                addressSnapshot = new IPAddressEx(address.GetAddressBytes(), address.ScopeId);
            }


            //roland start
            if (sourceAddress != null)
            {
                this.TestIsIpSupported(sourceAddress);
                if (sourceAddress.Equals(IPAddressEx.Any) || sourceAddress.Equals(IPAddressEx.IPv6Any))
                {
                    sourceAddress2 = null;
                }
                else
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        sourceAddress2 = new IPAddressEx(sourceAddress.GetAddressBytes());
                    }
                    else
                    {
                        sourceAddress2 = new IPAddressEx(sourceAddress.GetAddressBytes(), sourceAddress.ScopeId);
                    }
                }
            }
            else
            {
                sourceAddress2 = null;
            }
            //roland end


            (new NetworkInformationPermission(NetworkInformationAccess.Ping)).Demand();

            CheckStart(false);
            try
            {
                return InternalSend(
                    sourceAddress2,     //roland
                    addressSnapshot, buffer, timeout, options);
            }
            catch (Exception e)
            {
                throw new PingException(string.Format("PingEx.Send exception: {0}", e.Message), e);
            }
            finally
            {
                Finish(false);
            }
        }



        // internal method responsible for sending echo request on win2k and higher

        private PingReplyEx InternalSend(
            IPAddressEx sourceAddress,    //roland
            IPAddressEx address, byte[] buffer, int timeout, PingOptionsEx options)
        {

            ipv6 = (address.AddressFamily == AddressFamily.InterNetworkV6) ? true : false;
            sendSize = buffer.Length;

            //get and cache correct handle
            if (!ipv6 && handlePingV4 == null)
            {
                handlePingV4 = UnsafeNetInfoNativeMethodsEx.IcmpCreateFile();
                if (handlePingV4.IsInvalid)
                {
                    handlePingV4 = null;
                    throw new Win32Exception(); // Gets last error
                }
            }
            else if (ipv6 && handlePingV6 == null)
            {
                handlePingV6 = UnsafeNetInfoNativeMethodsEx.Icmp6CreateFile();
                if (handlePingV6.IsInvalid)
                {
                    handlePingV6 = null;
                    throw new Win32Exception(); // Gets last error
                }
            }


            //setup the options
            IPOptionsEx ipOptions = new IPOptionsEx(options);

            //setup the reply buffer
            if (replyBuffer == null)
            {
                replyBuffer = SafeLocalFreeEx.LocalAlloc(MaxUdpPacket);
            }

            //queue the event
            int error;

            try
            {
                //Copy user dfata into the native world
                SetUnmanagedStructures(buffer);

                if (!ipv6)
                {
                    if (sourceAddress == null)  //roland
                    {
                        error = (int)UnsafeNetInfoNativeMethodsEx.IcmpSendEcho2(handlePingV4, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)address.m_Address, requestBuffer, (ushort)buffer.Length, ref ipOptions, replyBuffer, MaxUdpPacket, (uint)timeout);
                    }
                    //roland start
                    else
                    {
                        error = (int)UnsafeNetInfoNativeMethodsEx.IcmpSendEcho2Ex(handlePingV4, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)sourceAddress.m_Address, (uint)address.m_Address, requestBuffer, (ushort)buffer.Length, ref ipOptions, replyBuffer, ReplyBufferSize, (uint)timeout);
                    }
                    //roland end
                }
                else
                {
                    IPEndPointEx ep = new IPEndPointEx(address, 0);
                    SocketAddressEx remoteAddr = ep.Serialize();
                    byte[] sourceAddr = new byte[28];   //roland TODO: IPv6 sourceAddress support
                    error = (int)UnsafeNetInfoNativeMethodsEx.Icmp6SendEcho2(handlePingV6, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, sourceAddr, remoteAddr.m_Buffer, requestBuffer, (ushort)buffer.Length, ref ipOptions, replyBuffer, MaxUdpPacket, (uint)timeout);
                }
            }
            catch
            {
                UnregisterWaitHandle();
                throw;
            }

            //need this if something is bogus.
            if (error == 0)
            {
                error = (int)Marshal.GetLastWin32Error();

                // Cleanup
                FreeUnmanagedStructures();
                UnregisterWaitHandle();

                if (error < (int)IPStatus.DestinationNetworkUnreachable // Min
                    || error > (int)IPStatus.DestinationScopeMismatch) // Max // Out of IPStatus range
                {
                    if (error == 11050)   //roland
                        throw new Win32Exception("Error 10150 - IP_GENERAL_FAILURE: Check Your firewall. Can also happen when IcmpSendEcho() receives a too small \"RequestData\"-buffer."); 	//roland
                    else
                        throw new Win32Exception(error);
                }

                return new PingReplyEx((IPStatus)error); // Synchronous IPStatus errors 
            }

            FreeUnmanagedStructures();

            //return the reply
            PingReplyEx reply;
            if (ipv6)
            {
                Icmp6EchoReplyEx icmp6Reply = (Icmp6EchoReplyEx)Marshal.PtrToStructure(replyBuffer.DangerousGetHandle(), typeof(Icmp6EchoReplyEx));
                reply = new PingReplyEx(icmp6Reply, replyBuffer.DangerousGetHandle(), sendSize);
            }
            else
            {
                IcmpEchoReplyEx icmpReply = (IcmpEchoReplyEx)Marshal.PtrToStructure(replyBuffer.DangerousGetHandle(), typeof(IcmpEchoReplyEx));
                reply = new PingReplyEx(icmpReply);
            }

            // IcmpEchoReply still has an unsafe IntPtr reference into replybuffer
            // and replybuffer was being freed prematurely by the GC, causing AccessViolationExceptions.
            GC.KeepAlive(replyBuffer);

            return reply;
        }

        // Tests if the current machine supports the given ip protocol family
        private void TestIsIpSupported(IPAddressEx ip)
        {
            // Catches if IPv4 has been uninstalled on Vista+
            if (ip.AddressFamily == AddressFamily.InterNetwork && !SocketEx.OSSupportsIPv4)
                throw new NotSupportedException("IPv4 not installed");
            // Catches if IPv6 is not installed on XP
            else if ((ip.AddressFamily == AddressFamily.InterNetworkV6 && !SocketEx.OSSupportsIPv6))
                throw new NotSupportedException("IPv6 not installed");
        }

        // copies sendbuffer into unmanaged memory for async icmpsendecho apis
        private unsafe void SetUnmanagedStructures(byte[] buffer)
        {
            requestBuffer = SafeLocalFreeEx.LocalAlloc(buffer.Length);
            byte* dst = (byte*)requestBuffer.DangerousGetHandle();
            for (int i = 0; i < buffer.Length; ++i)
            {
                dst[i] = buffer[i];
            }
        }

        // release the unmanaged memory after ping completion
        void FreeUnmanagedStructures()
        {
            if (requestBuffer != null)
            {
                requestBuffer.Close();
                requestBuffer = null;
            }
        }
    }

}


