#include <windows.h>
#include <fstream>
#include <string>
#include <direct.h>
#include <sys/stat.h>

// Export the function for DLL usage
extern "C" __declspec(dllexport) void process()
{
    std::string filePath = "c:\\temp\\a";
    
    // Ensure the directory exists
    std::string directory = "c:\\temp";
    
    struct stat info;
    if (stat(directory.c_str(), &info) != 0) {
        // Directory doesn't exist, create it
        _mkdir(directory.c_str());
    }
    
    // Create the file
    std::ofstream file(filePath);
    file.close();
}

// DLL Entry Point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
