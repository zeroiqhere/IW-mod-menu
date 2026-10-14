#include "SessionFiles.h"

#include <algorithm>
#include <cwctype>
#include <stdexcept>

namespace iw::overlay::runtime {
namespace {
class FileHandle {
public:
    explicit FileHandle(HANDLE handle) : handle_(handle) {}
    ~FileHandle() { if (handle_ != INVALID_HANDLE_VALUE) CloseHandle(handle_); }
    HANDLE Get() const { return handle_; }
    FileHandle(const FileHandle&) = delete;
    FileHandle& operator=(const FileHandle&) = delete;
private:
    HANDLE handle_;
};

void Check(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}

BY_HANDLE_FILE_INFORMATION Information(HANDLE handle) {
    BY_HANDLE_FILE_INFORMATION info{};
    Check(handle != INVALID_HANDLE_VALUE && GetFileInformationByHandle(handle, &info), "Session file information is unavailable");
    Check((info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0, "Session paths must not be reparse points");
    return info;
}

HANDLE OpenDirectory(const std::wstring& path) {
    return CreateFileW(path.c_str(), FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING,
        FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr);
}
}

SessionFiles::SessionFiles(const std::wstring& directory) : directory_(directory) {
    // Limit paths to a supplied local absolute directory instead of accepting device or share paths
    Check(directory.size() >= 3 && directory.size() <= 1000 && std::iswalpha(directory[0])
        && directory[1] == L':' && (directory[2] == L'\\' || directory[2] == L'/'), "Invalid local session directory path");
    for (wchar_t character : directory) Check(character >= 32, "Invalid session directory character");
    while (directory_.size() > 3 && (directory_.back() == L'\\' || directory_.back() == L'/')) directory_.pop_back();
    HANDLE opened = OpenDirectory(directory_);
    FileHandle temporary(opened);
    directoryIdentity_ = Information(opened);
    Check((directoryIdentity_.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0, "The session path is not a directory");
    Check(DuplicateHandle(GetCurrentProcess(), opened, GetCurrentProcess(), &directoryHandle_, 0, FALSE, DUPLICATE_SAME_ACCESS), "The session directory handle could not be retained");
}

SessionFiles::~SessionFiles() {
    if (directoryHandle_ != INVALID_HANDLE_VALUE) CloseHandle(directoryHandle_);
}

void SessionFiles::VerifyDirectory() const {
    // Reject a directory replacement before accessing its fixed protocol files
    FileHandle reopened(OpenDirectory(directory_));
    const auto current = Information(reopened.Get());
    Check((current.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0
        && current.dwVolumeSerialNumber == directoryIdentity_.dwVolumeSerialNumber
        && current.nFileIndexHigh == directoryIdentity_.nFileIndexHigh
        && current.nFileIndexLow == directoryIdentity_.nFileIndexLow, "The session directory identity changed");
}

std::string SessionFiles::Read(const wchar_t* name) const {
    VerifyDirectory();
    std::wstring path = directory_ + L"\\" + name;
    FileHandle file(CreateFileW(path.c_str(), GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING,
        FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_SEQUENTIAL_SCAN, nullptr));
    auto info = Information(file.Get());
    Check((info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0, "A session file is a directory");
    LARGE_INTEGER length{};
    Check(GetFileSizeEx(file.Get(), &length) && length.QuadPart > 0
        && length.QuadPart <= static_cast<LONGLONG>(MaximumFileBytes), "Session file size is invalid");
    std::string bytes(static_cast<std::size_t>(length.QuadPart), '\0');
    DWORD received = 0;
    Check(ReadFile(file.Get(), bytes.data(), static_cast<DWORD>(bytes.size()), &received, nullptr)
        && received == bytes.size(), "The session file read was incomplete");
    // Reject files that grew while the bounded snapshot was being read
    char extra = 0;
    Check(ReadFile(file.Get(), &extra, 1, &received, nullptr) && received == 0, "The session file changed during its read");
    return bytes;
}

void SessionFiles::Write(const wchar_t* temporary, const wchar_t* destination, const std::string& bytes) const {
    Check(!bytes.empty() && bytes.size() <= MaximumFileBytes, "Protocol output size is invalid");
    VerifyDirectory();
    std::wstring temporaryPath = directory_ + L"\\" + temporary;
    std::wstring destinationPath = directory_ + L"\\" + destination;
    // Open any old temporary file itself and reject links before replacing its bytes
    {
        FileHandle file(CreateFileW(temporaryPath.c_str(), GENERIC_WRITE, 0, nullptr, OPEN_ALWAYS,
            FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        auto info = Information(file.Get());
        Check((info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0, "Protocol temporary path is a directory");
        LARGE_INTEGER beginning{};
        Check(SetFilePointerEx(file.Get(), beginning, nullptr, FILE_BEGIN) && SetEndOfFile(file.Get()), "Protocol temporary file could not be reset");
        DWORD written = 0;
        Check(WriteFile(file.Get(), bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr)
            && written == bytes.size() && FlushFileBuffers(file.Get()), "Protocol output could not be written completely");
    }
    // Proton may deny replacement while a correctly shared reader briefly holds the old target
    // https://github.com/ValveSoftware/wine/blob/experimental_11.0/server/fd.c#L2930
    const ULONGLONG deadline = GetTickCount64() + 100;
    bool firstAttempt = true;
    while (firstAttempt || GetTickCount64() < deadline) {
        const bool initialAttempt = firstAttempt;
        firstAttempt = false;
        VerifyDirectory();
        DWORD attributes = GetFileAttributesW(destinationPath.c_str());
        if (attributes != INVALID_FILE_ATTRIBUTES) {
            // Invalid targets must fail immediately rather than being hidden by a sharing retry
            Check((attributes & (FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_READONLY)) == 0,
                "Protocol destination is not an ordinary writable file");
        } else {
            Check(GetLastError() == ERROR_FILE_NOT_FOUND, "Protocol destination could not be checked");
        }
        if (!initialAttempt && GetTickCount64() >= deadline) break;
        if (MoveFileExW(temporaryPath.c_str(), destinationPath.c_str(),
            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) return;
        const DWORD error = GetLastError();
        Check(error == ERROR_ACCESS_DENIED || error == ERROR_SHARING_VIOLATION || error == ERROR_LOCK_VIOLATION,
            "Protocol output could not be replaced atomically");
        const ULONGLONG now = GetTickCount64();
        if (now >= deadline) break;
        // Retry only the completed temporary frame, with no repeated writes or unbounded delay
        Sleep(static_cast<DWORD>(std::min<ULONGLONG>(5, deadline - now)));
    }
    throw std::runtime_error("Protocol output replacement remained blocked after its bounded retry");
}

std::string SessionFiles::ReadState() const { return Read(L"state.json"); }
std::string SessionFiles::ReadBindings() const { return Read(L"bindings.json"); }
void SessionFiles::WriteCommand(const std::string& bytes) const { Write(L"command.tmp", L"command.json", bytes); }
void SessionFiles::WriteReady(const std::string& bytes) const { Write(L"runtime-ready.tmp", L"runtime-ready.json", bytes); }
void SessionFiles::WriteRendererStatus(const std::string& bytes) const { Write(L"renderer-status.tmp", L"renderer-status.json", bytes); }
void SessionFiles::WriteRuntimeError(const std::string& bytes) const { Write(L"runtime-error.tmp", L"runtime-error.json", bytes); }

}
