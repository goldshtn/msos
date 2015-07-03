# msos
This project provides a command-line environment a-la WinDbg for executing SOS commands without having SOS available. It is based on the ClrMD library that is essentially a managed replacement for SOS.

You should use this project when you don't have SOS available, or when you want a quick alternative to firing up WinDbg and locating SOS. One such situation is when debugging dumps from Windows Phone devices; Microsoft does not make the Windows Phone CoreCLR SOS publicly available at present. What's more, some msos commands already offer more information than their SOS counterparts.

To use msos, compile the project and run it from the command line with a dump file:

```msos -z myapp.dmp```

Type ```help``` to get a list of currently supported commands. Note that some options might currently be unsupported, and are marked as such by the built-in help.

Examples:

```
0> !dumpheap --type String$ --stat
Statistics:
MT                   Count      TotalSize  Class Name
000000006e21565c     14751      1046116    System.String
Total 14751 objects
Elapsed: 121ms

0> !ThreadPool
Total threads:   24
Running threads: 1
Idle threads:    10
Max threads:     1023
Min threads:     2
CPU utilization: 48% (estimated)
Elapsed: 2ms

0> !do 00000000017eddbc
Name:     System.Threading.TimerQueueTimer
MT:       000000006e209bc8
Size:     52(0x34) bytes
Assembly: C:\windows\system32\mscorlib.ni.dll
Value:    1847630792
Fields:
Offset   Type                 VT  Attr       Value                Name
0        ....TimerQueueTimer  0   instance   00000000017edc18     m_next
4        ....TimerQueueTimer  0   instance   0000000001810ec4     m_prev
8        ...ng.TimerCallback  0   instance   000000000155cef0     m_timerCallback
c        System.Object        0   instance   00000000017edd70     m_state
10       ...ExecutionContext  0   instance   000000000141f57c     m_executionContext
14       ...ading.WaitHandle  0   instance   0000000000000000     m_notifyWhenNoCallbacksRunning
18       System.Int32         1   instance   000000000202871b     m_startTicks
1c       System.UInt32        1   instance   00000000000003e8     m_dueTime
20       System.UInt32        1   instance   00000000ffffffff     m_period
24       System.Int32         1   instance   0000000000000000     m_callbacksRunning
28       System.Boolean       1   instance   False                m_canceled
17c      ....ContextCallback  0   instance   static               s_callCallbackInContext
   >> Domain:Value  000000000120e850:NotInit <<
Elapsed: 24ms
```
