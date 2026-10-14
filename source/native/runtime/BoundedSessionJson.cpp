#include "BoundedSessionJson.h"

#include <set>
#include <stdexcept>
#include <vector>

namespace iw::overlay::runtime {
nlohmann::json ReadBoundedSessionJson(const std::string& bytes, std::size_t maximumBytes) {
    if (bytes.empty() || bytes.size() > maximumBytes) {
        throw std::runtime_error("Session JSON exceeds its allowed size");
    }
    using Json = nlohmann::json;
    std::vector<std::set<std::string>> objectKeys;
    const auto inspect = [&](int depth, Json::parse_event_t event, Json& value) {
        // Container start reports its parent depth, so the root consumes the first level
        if ((event == Json::parse_event_t::object_start || event == Json::parse_event_t::array_start)
            && depth >= 8) {
            throw std::runtime_error("Session JSON exceeds its allowed depth");
        }
        if (event == Json::parse_event_t::object_start) objectKeys.emplace_back();
        if (event == Json::parse_event_t::key) {
            // Decoded names catch escaped duplicates without confusing separate row objects
            if (objectKeys.empty() || !objectKeys.back().insert(value.get<std::string>()).second) {
                throw std::runtime_error("Session JSON contains duplicate properties");
            }
        }
        if (event == Json::parse_event_t::object_end) {
            if (objectKeys.empty()) throw std::runtime_error("Session JSON has no matching object");
            objectKeys.pop_back();
        }
        // Returning false would silently remove data instead of rejecting the entire packet
        return true;
    };
    return Json::parse(bytes, inspect);
}
}
