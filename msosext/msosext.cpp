#include "engextcpp.hpp"

// TODO All hardcoded paths...

#import "C:\dev\msos\msoscore\bin\x86\Debug\msoscore.tlb" auto_rename

class EXT_CLASS : public ExtExtension
{
public:
	EXT_COMMAND_METHOD(msos);
private:
	void init_msos();
	msoscore::IMsosPtr msos_ptr_;
};

EXT_DECLARE_GLOBALS();

void EXT_CLASS::init_msos()
{
	if (msos_ptr_ != nullptr)
		return;

	using create_msos_t = void(__stdcall *)(
		IDebugClient*,
		msoscore::IMsos**
		);

	// TODO Do we need this?
	CoInitializeEx(nullptr, COINIT_MULTITHREADED);

	HMODULE hMsosLib = LoadLibrary(L"C:\\dev\\msos\\msoscore\\bin\\x86\\Debug\\msoscore.dll");
	create_msos_t create_msos = (create_msos_t)GetProcAddress(hMsosLib, "CreateMsos");
	create_msos(m_Client, &msos_ptr_);
}

EXT_COMMAND(msos, "Runs an msos command", "{{custom}}")
{
	init_msos();

	dprintf("msos command was invoked with arguments: %s\n", GetRawArgStr());
	dprintf("msos says: %s\n", (char const*)msos_ptr_->Echo(_bstr_t(GetRawArgStr())));
}
