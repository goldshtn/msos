using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static msos.NativeStructs;
using static msos.NativeMethods;

namespace msos
{
    /// <summary>
    /// Exract blocking objects information using WCT api:
    /// doc: https://msdn.microsoft.com/en-us/library/windows/desktop/ms681622(v=vs.85).aspx
    /// </summary>
    class WaitChainTraversal
    {
        public const int WCT_MAX_NODE_COUNT = 16;
        public const uint WCTP_GETINFO_ALL_FLAGS = 7;
        public const int WCT_OBJNAME_LENGTH = 128;

        /// <summary>
        /// Gets WCT information using WCT api by the given threadId (OS threadId)
        /// </summary>
        /// <param name="threadId"> ThreadWCTInfo  object</param>
        /// <returns>Thread id of thread target</returns>
        public ThreadWCTInfo GetBlockingObjects(uint threadId)
        {
            ThreadWCTInfo result = null;

            var g_WctIntPtr = OpenThreadWaitChainSession((int)WCT_SESSION_OPEN_FLAGS.WCT_SYNC_OPEN_FLAG, 0);


            WAITCHAIN_NODE_INFO[] NodeInfoArray = new WAITCHAIN_NODE_INFO[WCT_MAX_NODE_COUNT];


            int isCycle = 0;
            int Count = WCT_MAX_NODE_COUNT;

            // Make a synchronous WCT call to retrieve the wait chain.
            bool waitChainResult = GetThreadWaitChain(g_WctIntPtr,
                                    IntPtr.Zero,
                                    WCTP_GETINFO_ALL_FLAGS,
                                    threadId, ref Count, NodeInfoArray, out isCycle);

            // Check if the wait chain is too big for the array we passed in.
            if (Count > WCT_MAX_NODE_COUNT)
            {
                Count = WCT_MAX_NODE_COUNT;
            }

            if (waitChainResult)
            {
                result = HandleGetThreadWaitChainRsult(threadId, Count, NodeInfoArray, isCycle);
            }

            CloseThreadWaitChainSession(g_WctIntPtr);

            return result;
        }

        private ThreadWCTInfo HandleGetThreadWaitChainRsult(uint threadId, int Count, WAITCHAIN_NODE_INFO[] NodeInfoArray, int isCycle)
        {
            WAITCHAIN_NODE_INFO[] waitchain = new WAITCHAIN_NODE_INFO[Count];
            Array.Copy(NodeInfoArray, waitchain, Count);

            ThreadWCTInfo result = new ThreadWCTInfo(isCycle == 1, threadId, waitchain);

            return result;
        }
    }

    class ThreadWCTInfo
    {
        public ThreadWCTInfo(bool isDeadLock, uint threadId, WAITCHAIN_NODE_INFO[] info)
        {
            IsDeadLocked = isDeadLock;
            ThreadId = threadId;

            WctBlockingObjects = new List<WaitChainInfoObject>();

            foreach (var item in info)
            {
                var block = new WaitChainInfoObject(item);
                WctBlockingObjects.Add(block);
            }
        }

        /// <summary>
        /// Specifies whether the Wait Chain is Cyclic - Deadlock
        /// </summary>
        public bool IsDeadLocked { get; private set; }
        /// <summary>
        /// OS Id of the thread
        /// </summary>
        public uint ThreadId { get; private set; }

        /// <summary>
        /// Thread blocking objects
        /// </summary>
        public List<WaitChainInfoObject> WctBlockingObjects { get; private set; }
    }

    class WaitChainInfoObject
    {
        public WaitChainInfoObject(WAITCHAIN_NODE_INFO item)
        {
            ObjectStatus = item.ObjectStatus;
            ObjectType = item.ObjectType;

            TimeOut = item.Union.LockObject.Timeout;
            AlertTable = item.Union.LockObject.Alertable;

            if (item.ObjectType == WCT_OBJECT_TYPE.WctThreadType)
            { 
                //Use the ThreadObject part of the union
                this.ThreadId = item.Union.ThreadObject.ThreadId;
                this.ProcessId = item.Union.ThreadObject.ProcessId;
                this.ContextSwitches = item.Union.ThreadObject.ContextSwitches;
                this.WaitTime = item.Union.ThreadObject.WaitTime;
            }
            else
            {
                //Use the LockObject part of the union
                unsafe
                {
                    ObjectName = Marshal.PtrToStringUni((IntPtr)item.Union.LockObject.ObjectName);
                }
            }

        }

        public WCT_OBJECT_STATUS ObjectStatus { get; private set; }
        public WCT_OBJECT_TYPE ObjectType { get; private set; }

        /// <summary>
        /// Is Current Objects Status is WCT_OBJECT_STATUS.WctStatusBlocked
        /// </summary>
        public bool IsBlocked { get { return ObjectStatus == WCT_OBJECT_STATUS.WctStatusBlocked; } }
        /// <summary>
        /// The thread identifier. For COM and ALPC, this member can be 0.
        /// </summary>
        public uint ThreadId { get; private set; }
        /// <summary>
        /// The process identifier.
        /// </summary>
        public uint ProcessId { get; private set; }
        /// <summary>
        /// The name of the object. Object names are only available for certain object, such as mutexes. 
        /// If the object does not have a name, this member is an empty string.
        /// </summary>
        public string ObjectName { get; private set; }
        /// <summary>
        /// The wait time.
        /// </summary>
        public uint WaitTime { get; private set; }
        /// <summary>
        /// The number of context switches.
        /// </summary>
        public uint ContextSwitches { get; private set; }
        /// <summary>
        /// This member is reserved for future use.
        /// </summary>
        public ulong TimeOut { get; private set; }
        /// <summary>
        /// This member is reserved for future use.
        /// </summary>
        public uint AlertTable { get; private set; }
    }
}
