//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


//determines which options will be used for sending icmp requests, as well as what options
//were set in the returned icmp reply.

namespace System.Net.NetworkInformation
{
    // Represent the possible ip options used for the icmp packet
    public class PingOptionsEx
    {
        const int DontFragmentFlag = 2;
        int ttl = 128;
        bool dontFragment;

        internal PingOptionsEx(IPOptionsEx options)
        {
            this.ttl = options.ttl;
            this.dontFragment = ((options.flags & DontFragmentFlag) > 0 ? true : false);
        }

        public PingOptionsEx(int ttl, bool dontFragment)
        {
            if (ttl <= 0)
            {
                throw new ArgumentOutOfRangeException("ttl");
            }

            this.ttl = ttl;
            this.dontFragment = dontFragment;
        }

        public PingOptionsEx()
        {
        }

        public int Ttl
        {
            get
            {
                return ttl;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                ttl = value; //useful to discover routes
            }
        }

        public bool DontFragment
        {
            get
            {
                return dontFragment;
            }
            set
            {
                dontFragment = value;  //useful for discovering mtu
            }
        }
    }
}

