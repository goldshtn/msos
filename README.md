# msos
This project provides a command-line environment a-la WinDbg for executing SOS commands without having SOS available. It is based on the ClrMD library that is essentially a managed replacement for SOS.

You should use this project when you don't have SOS available, or when you want a quick alternative to firing up WinDbg and locating SOS. Already, some msos commands offer more information than their SOS counterparts.

To use msos, compile the project and run it from the command line with a dump file:

```msos -z myapp.dmp```

Type ```help``` to get a list of currently supported commands. Note that some options might currently be unsupported, and are marked as such by the built-in help.
