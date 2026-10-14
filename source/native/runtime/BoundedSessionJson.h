#pragma once

#include <cstddef>
#include <string>
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
// Private packets share resource limits before their individual schemas are checked
nlohmann::json ReadBoundedSessionJson(const std::string& bytes, std::size_t maximumBytes);
}
