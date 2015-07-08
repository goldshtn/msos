# msos
This project provides a command-line environment a-la WinDbg for executing SOS commands without having SOS available. It is based on the ClrMD library that is essentially a managed replacement for SOS.

Build status: [![Build status](https://ci.appveyor.com/api/projects/status/gla95e3t81oodbvh?svg=true)](https://ci.appveyor.com/project/goldshtn/msos)

You should use this project when you don't have SOS available, or when you want a quick alternative to firing up WinDbg and locating SOS. One such situation is when debugging dumps from Windows Phone devices; Microsoft does not make the Windows Phone CoreCLR SOS publicly available at present. What's more, some msos commands already offer more information than their SOS counterparts. Especially cool is the ```!hq``` command, which compiles an arbitrary dynamic query over heap objects and classes.

To use msos, compile the project and run it from the command line with a dump file:

```msos -z myapp.dmp```

...or a live target:

```msos --pn myprocess.exe```

Type ```help``` to get a list of currently supported commands. Note that some options might currently be unsupported, and are marked as such by the built-in help.

Examples:

```
0> !dumpheap --type String$ --stat
Statistics:
MT                   Count      TotalSize  Class Name
000000006e21565c     14751      1046116    System.String
Total 14751 objects
Elapsed: 121ms

1> !hq from o in ObjectsOfType("System.IO.StreamReader") select new { SR = o.GetValue(), CP = o.encoding.m_codePage }
SR                                                  CP
33307512                                            437
Rows: 1
Time: 604 ms, Memory start: 124.930kb, Memory end: 157.840kb, Memory delta: +32.910kb

1> !hq Class("System.String").__Fields
Name                                                Type
m_stringLength                                      System.Int32
m_firstChar                                         System.Char
Rows: 2
Time: 530 ms, Memory start: 124.145kb, Memory end: 157.254kb, Memory delta: +33.109kb

1> !hq from s in ObjectsOfType("System.String") where s.__Size > 100 select new { Str = (string)s, Size = s.__Size }
Str                                                 Size
...C:\Temp\VSDebugging\bin\Debug\VSDebugging.exe.C  118
C:\Temp\VSDebugging\bin\Debug\VSDebugging.exe       104
C:\Windows\Microsoft.NET\Framework\v4.0.30319\      106
...C:\Windows\Microsoft.NET\Framework\v4.0.30319\c  148
...System\CurrentControlSet\Control\Nls\RegionMapp  114
Rows: 5
Time: 697 ms, Memory start: 125.262kb, Memory end: 160.242kb, Memory delta: +34.980kb

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

0> !gcroot 0000000001581a84
000000000101131C -> 00000000015801C4 RefCount handle
        -> 00000000015801C4 System.Runtime.InteropServices.WindowsRuntime.CustomPropertyImpl
        -> 0000000001577948 System.Reflection.RuntimePropertyInfo
        -> 000000000156A18C System.RuntimeType+RuntimeTypeCache
        -> 0000000001574E0C System.RuntimeType+RuntimeTypeCache+MemberInfoCache<System.Reflection.RuntimePropertyInfo>
        -> 0000000001581A70 System.Reflection.CerHashtable+Table<System.String,System.Reflection.RuntimePropertyInfo[]>
        -> 0000000001581A84 System.String[]

Elapsed: 635ms
```
