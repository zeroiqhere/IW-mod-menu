#pragma once

#include "RuntimeSchema.h"

namespace iw::overlay::runtime {

// Only fixed protocol filenames are available inside the supplied session directory
class SessionFiles {
public:
    explicit SessionFiles(const std::wstring& directory);
    ~SessionFiles();
    SessionFiles(const SessionFiles&) = delete;
    SessionFiles& operator=(const SessionFiles&) = delete;
    std::string ReadState() const;
    std::string ReadBindings() const;
    void WriteCommand(const std::string& bytes) const;
    void WriteReady(const std::string& bytes) const;
    void WriteRendererStatus(const std::string& bytes) const;
    void WriteRuntimeError(const std::string& bytes) const;
private:
    std::wstring directory_;
    HANDLE directoryHandle_ = INVALID_HANDLE_VALUE;
    BY_HANDLE_FILE_INFORMATION directoryIdentity_{};
    void VerifyDirectory() const;
    std::string Read(const wchar_t* name) const;
    void Write(const wchar_t* temporary, const wchar_t* destination, const std::string& bytes) const;
};

}
