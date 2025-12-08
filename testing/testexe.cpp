#include <windows.h>
#include <fstream>
#include <iostream>
#include <string>
#include <direct.h>
#include <sys/stat.h>

int main()
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
    
    std::cout << "File created successfully: " << filePath << std::endl;
    
    return 0;
}
