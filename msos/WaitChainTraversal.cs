using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static msos.NativeStructs;
using static msos.NativeMethods;
using System.Linq;

namespace msos
{
    /// <summary>
    /// Extract blocking objects information using the Windows Wait Chain Traversal API.
    /// </summary>
    class WaitChainTraversal
    {
        private const int WCT_MAX_NODE_COUNT = 16;
        private const uint WCTP_GETINFO_ALL_FLAGS = 7;
        private const int WCT_OBJNAME_LENGTH = 128;

        /// <summary>
        /// Gets WCT information for the specified thread.
        /// </summary>
        public ThreadWCTInfo GetBlockingObjects(uint osThreadId)
        {
            IntPtr wctSession = IntPtr.Zero;
            try
            {
                wctSession = OpenThreadWaitChainSession((int)WCT_SESSION_OPEN_FLAGS.WCT_SYNC_OPEN_FLAG, 0);
                if (wctSession == IntPtr.Zero)
                    return null;

                WAITCHAIN_NODE_INFO[] nodes = new WAITCHAIN_NODE_INFO[WCT_MAX_NODE_COUNT];
                int isCycle = 0;
                int count = nodes.Length;
                if (GetThreadWaitChain(wctSession, IntPtr.Zero, WCTP_GETINFO_ALL_FLAGS,
                                       osThreadId, ref count, nodes, out isCycle))
                {
                    return new ThreadWCTInfo(isCycle == 1, osThreadId, nodes.Take(count).ToArray());
                }
            }
            finally
            {
                if (wctSession != IntPtr.Zero)
                    CloseThreadWaitChainSession(wctSession);
            }

            return null;
        }
    }

    class ThreadWCTInfo
    {
        public ThreadWCTInfo(bool isDeadLock, uint threadId, WAITCHAIN_NODE_INFO[] info)
        {
            IsDeadlocked = isDeadLock;
            OSThreadId = threadId;
            WaitChain.AddRange(info.Select(i => new WaitChainInfoObject(i)));
        }

        public bool IsDeadlocked { get; private set; }
        public uint OSThreadId { get; private set; }
        public List<WaitChainInfoObject> WaitChain { get; } = new List<WaitChainInfoObject>();
    }

    class WaitChainInfoObject
    {
        public WaitChainInfoObject(WAITCHAIN_NODE_INFO item)
        {
            ObjectStatus = item.ObjectStatus;
            ObjectType = item.ObjectType;

            TimeOut = item.Union.LockObject.Timeout;
            Alertable = item.Union.LockObject.Alertable;

            if (item.ObjectType == WCT_OBJECT_TYPE.WctThreadType)
            {
                OSThreadId = item.Union.ThreadObject.ThreadId;
                OSProcessId = item.Union.ThreadObject.ProcessId;
                ContextSwitches = item.Union.ThreadObject.ContextSwitches;
                WaitTime = item.Union.ThreadObject.WaitTime;
            }
            else
            {
                unsafe
                {
                    ObjectName = Marshal.PtrToStringUni((IntPtr)item.Union.LockObject.ObjectName);
                }
            }

        }

        public WCT_OBJECT_STATUS ObjectStatus { get; private set; }
        public WCT_OBJECT_TYPE ObjectType { get; private set; }

        public bool IsBlocked { get { return ObjectStatus == WCT_OBJECT_STATUS.WctStatusBlocked; } }
        public uint OSThreadId { get; private set; }
        public uint OSProcessId { get; private set; }

        /// <summary>
        /// The name of the object. Object names are only available for certain object, such as mutexes. 
        /// If the object does not have a name, this member is an empty string.
        /// </summary>
        public string ObjectName { get; private set; }
        public uint WaitTime { get; private set; }
        public uint ContextSwitches { get; private set; }

        /// <summary>
        /// This member is reserved for future use.
        /// </summary>
        public ulong TimeOut { get; private set; }
        /// <summary>
        /// This member is reserved for future use.
        /// </summary>
        public uint Alertable { get; private set; }
    }
}
