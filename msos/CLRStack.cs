using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!CLRStack", HelpText="Displays the managed call stack of the current thread.")]
    class CLRStack : ICommand
    {
        [Option('a', HelpText = "Display method arguments and local variables.")]
        public bool DisplayAllStackValues { get; set; }

        private CommandExecutionContext _context;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            var thread = context.CurrentThread;
            if (thread == null)
            {
                context.WriteError("There is no current managed thread");
                return;
            }

            thread.WriteCurrentStackTraceToContext(context);
            DisplayArgumentsAndLocals();
        }

        private void DisplayArgumentsAndLocals()
        {
            Type runtimeType = _context.Runtime.GetType();
            FieldInfo dacField = runtimeType.GetField("m_dacInterface", BindingFlags.NonPublic | BindingFlags.Instance);
            object dacInterface = dacField.GetValue(_context.Runtime);
            IXCLRDataProcess ixclrDataProcess = (IXCLRDataProcess)dacInterface;

            object tmp;
            if (HR.Failed(ixclrDataProcess.GetTaskByOSThreadID(_context.CurrentThread.OSThreadId, out tmp)))
                return;

            IXCLRDataTask task = (IXCLRDataTask)tmp;
            if (HR.Failed(task.CreateStackWalk(0xf, out tmp)))
                return;

            IXCLRDataStackWalk stackWalk = (IXCLRDataStackWalk)tmp;
            do
            {
                if (HR.Failed(stackWalk.GetFrame(out tmp)))
                    continue;

                IXCLRDataFrame frame = (IXCLRDataFrame)tmp;
                uint numArgs, numLocals;
                if (HR.Failed(frame.GetNumArguments(out numArgs)) || HR.Failed(frame.GetNumLocalVariables(out numLocals)))
                    continue;

                // TODO Display these in sync with the frames in the stack trace

                _context.WriteLine("ARGS:");
                for (uint argIdx = 0; argIdx < numArgs; ++argIdx)
                {
                    StringBuilder argName = new StringBuilder(1024);
                    uint argNameLen;
                    if (HR.Failed(frame.GetArgumentByIndex(argIdx, out tmp, (uint)argName.Capacity, out argNameLen, argName)))
                        continue;

                    DisplayValue(argName.ToString(), (IXCLRDataValue)tmp);
                }

                _context.WriteLine("LOCALS:");
                for (uint lclIdx = 0; lclIdx < numLocals; ++lclIdx)
                {
                    StringBuilder lclName = new StringBuilder(1024);
                    uint lclNameLen;
                    if (HR.Failed(frame.GetLocalVariableByIndex(lclIdx, out tmp, (uint)lclName.Capacity, out lclNameLen, lclName)))
                        continue;

                    DisplayValue(lclName.ToString(), (IXCLRDataValue)tmp);
                }
            }
            while (0 == stackWalk.Next());
        }

        private void DisplayValue(string name, IXCLRDataValue value)
        {
            object tmp;

            ulong size;
            if (HR.Failed(value.GetSize(out size)))
                return;

            int getTypeHr = value.GetType(out tmp);
            if (getTypeHr == HR.S_FALSE)
            {
                // For reference types, GetType returns S_FALSE and we need to call GetAssociatedType
                // to retrieve the type that the reference points to.
                getTypeHr = value.GetAssociatedType(out tmp);
            }

            if (getTypeHr != HR.S_OK)
                return;

            IXCLRDataTypeInstance typeInstance = (IXCLRDataTypeInstance)tmp;
            StringBuilder typeName = new StringBuilder(2048);
            uint typeNameLen;
            if (HR.Failed(typeInstance.GetName(0 /*TODO flags*/, (uint)typeName.Capacity, out typeNameLen, typeName)))
                return;

            _context.WriteLine("\t{0} ({1}, size {2})", name, typeName.ToString(), size);

            // TODO For inner types, the outer type is not returned. To get a ClrType reliably, need
            // to see if the IXCLRDataTypeDefinition can help. If not, can go through the route of 
            // getting the object reference and then reading the type from it. If it's null, we don't
            // care anyway, and if it's valid, we'll get the type from ClrHeap.GetObjectType.

            // TODO Get the value and display it
        }
    }
}
