#include "engextcpp.hpp"

#import "..\msos\bin\x86\Debug\msos.tlb" auto_rename

class EXT_CLASS : public ExtExtension
{
public:
	EXT_CLASS();
	EXT_COMMAND_METHOD(msos);
private:
	Msos::IMsosPtr msos_ptr_;
};

EXT_DECLARE_GLOBALS();

EXT_CLASS::EXT_CLASS()
{
	using create_msos_t = void(__stdcall *)(Msos::IMsos**);

	// TODO Do we need this?
	CoInitializeEx(nullptr, COINIT_MULTITHREADED);

	HMODULE hMsosLib = LoadLibrary(L"C:\\dev\\msos\\msos\\bin\\x86\\Debug\\msos.exe");
	create_msos_t create_msos = (create_msos_t)GetProcAddress(hMsosLib, "CreateMsos");
	create_msos(&msos_ptr_);

	// TODO Use Rodney Viana's technique to load msos.exe and get the COM target out of it
	// Internally, it should create a DataTarget on top of the provided IDebugClient (m_Client)
	// http://blogs.msdn.com/b/rodneyviana/archive/2015/08/24/pure-native-c-consuming-net-classes-without-com-registration.aspx
}

EXT_COMMAND(msos, "Runs an msos command", "{{custom}}")
{
	dprintf("msos command was invoked with arguments: %s\n", GetRawArgStr());
	dprintf("msos says: %s\n", (char const*)msos_ptr_->Echo(_bstr_t(GetRawArgStr())));
	// TODO Talk to the msos COM object
}
