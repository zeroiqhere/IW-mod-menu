#include "../../native/render/rows/RowLabelFit.h"
#include "Checks.h"
#include <limits>
namespace iw::overlay::tests {
namespace {
float ByteWidth(std::string_view text) { return static_cast<float>(text.size()); }
} // namespace
void AddRowLabelChecks(Checks &checks) {
    using namespace rows;
    checks.emplace_back("Fitting and exact-fit labels keep every original byte", [] {
        for (float available : {5.f, 10.f}) {
            const auto label = FitRowLabel("Hello", available, ByteWidth);
            Require(label.valid && !label.ellipsized && label.Text() == "Hello", "Fitting label changed");
        }
    });
    checks.emplace_back("Overflowing ASCII uses the longest fitting prefix plus ellipsis", [] {
        const auto label = FitRowLabel("abcdefghij", 7, ByteWidth);
        Require(label.valid && label.ellipsized && label.Text() == "abcd...",
                "Prefix was not the longest fit");
    });
    checks.emplace_back("Multibyte labels end only at complete UTF-8 prefix boundaries", [] {
        const auto label = FitRowLabel("é界🙂abcdef", 9, ByteWidth);
        Require(label.valid && label.Text() == "é界...", "UTF-8 character was split");
    });
    checks.emplace_back("Bounded labels do not split a character at the byte limit", [] {
        const std::string input = std::string(511, 'a') + "🙂tail";
        const auto label = FitRowLabel(input, 1000, ByteWidth);
        Require(label.valid && label.length == 511 && label.Text() == std::string(511, 'a'),
                "Byte cap split UTF-8");
    });
    checks.emplace_back("Insufficient room omits the label without partial ellipsis", [] {
        for (float available : {0.f, 2.f}) {
            const auto label = FitRowLabel("abcdef", available, ByteWidth);
            Require(label.valid && label.ellipsized && label.Text().empty(), "Partial dots were drawn");
        }
        Require(FitRowLabel("abcdef", 3, ByteWidth).Text() == "...", "Exact dots fit was missed");
    });
    checks.emplace_back("Invalid available widths and invalid font measurements fail closed", [] {
        const auto nan = std::numeric_limits<float>::quiet_NaN();
        for (float available : {-1.f, nan, std::numeric_limits<float>::infinity()}) {
            Require(!FitRowLabel("abc", available, ByteWidth).valid, "Invalid width accepted");
        }
        for (float measured : {-1.f, nan, std::numeric_limits<float>::infinity()}) {
            Require(!FitRowLabel("abc", 5, [=](auto) { return measured; }).valid,
                    "Invalid measurement accepted");
        }
        const auto invalidCandidate =
            FitRowLabel("abcdef", 4, [](auto text) { return text == "..." ? -1.f : ByteWidth(text); });
        Require(!invalidCandidate.valid && invalidCandidate.Text().empty(),
                "Invalid dots measurement escaped");
        unsigned matchingCalls = 0;
        const auto unstable = FitRowLabel("abcdefghij", 7, [&](auto text) {
            return text == "abcd..." && ++matchingCalls > 1 ? 100.f : ByteWidth(text);
        });
        Require(!unstable.valid && unstable.Text().empty(),
                "Changed final measurement returned an overwide prefix");
    });
    checks.emplace_back("Fixed-buffer results own their text and use bounded measurement work", [] {
        unsigned calls = 0;
        auto label = FitRowLabel(std::string(512, 'a'), 150, [&](auto text) {
            ++calls;
            return ByteWidth(text);
        });
        auto copy = label;
        label.bytes.fill('x');
        Require(copy.valid && copy.Text() == std::string(147, 'a') + "..." && calls <= 13,
                "Fitter retained a foreign view or scanned every prefix");
    });
    checks.emplace_back("Empty labels do not invent an ellipsis", [] {
        const auto label = FitRowLabel("", 0, ByteWidth);
        Require(label.valid && !label.ellipsized && label.Text().empty(), "Empty label gained dots");
    });
}
} // namespace iw::overlay::tests
