#include "FooterObservationGraphTests.h"
#include "FooterObservationGraphExpected.h"
#include "../../withdrawal/fixtures/FooterWithdrawalFixtureTests.h"
#include "../../../../../native/render/instructions/observation/InstructionRenderPhase.h"
#include "../../../../../native/runtime/ControlLabels.h"
#include "../../../../native-layout/BackendPreview.h"
#include <algorithm>
#include <memory>
#include <stdexcept>
#include <string>
namespace iw::overlay::tests::footer_observation {
namespace {
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
bool Same(layout::Color a, layout::Color b) {
    return a.red == b.red && a.green == b.green && a.blue == b.blue && a.alpha == b.alpha;
}
const footer_withdrawal::Expected& Expected(const char* name) {
    const auto found = std::find_if(footer_withdrawal::Profiles.begin(), footer_withdrawal::Profiles.end(),
        [name](const auto& profile) { return profile.name == std::string(name); });
    Require(found != footer_withdrawal::Profiles.end(), "The literal fault schedule must name a real producer packet");
    return *found;
}
MenuSnapshot Read(const std::filesystem::path& directory, const footer_withdrawal::Expected& expected, bool preview) {
    // The existing checker compares both readers against literal epochs, raw rows and captured owner facts
    auto state = footer_withdrawal::ReadAndCheck(directory, expected);
    if (!preview) return state;
    const runtime::Bindings bindings{{2},{86,6},{2,38},{1,40},{70},{51},{52},{1},{38},{40},{71},{69},{16},{17},{32}};
    const auto labels = runtime::DescribeOpeningActions(bindings);
    return ReadBackendPreview(directory / expected.name, &labels);
}
void Unchanged(const MenuSnapshot& before, const MenuSnapshot& after) {
    Require(before.isOpen == after.isOpen && before.navigationRevision == after.navigationRevision
        && before.menuRevision == after.menuRevision && before.selectedIndex == after.selectedIndex,
        "Graphics observation cannot change the confirmed navigation or cursor");
    Require(before.localMenuEligibility == after.localMenuEligibility && before.accent == after.accent
        && before.smoothRainbowTheme == after.smoothRainbowTheme && before.rows.size() == after.rows.size(),
        "Graphics observation cannot change game permission, palette or row inventory");
    for (std::size_t i = 0; i < before.rows.size(); ++i) {
        Require(before.rows[i].label == after.rows[i].label && before.rows[i].value == after.rows[i].value
            && before.rows[i].enabled == after.rows[i].enabled && before.rows[i].isSpacer == after.rows[i].isSpacer
            && before.rows[i].openingAction == after.rows[i].openingAction,
            "Graphics failure cannot rewrite a raw feature value or action capability");
    }
}
void Check(const InstructionPaintObservation& observation, layout::Color outline, layout::Color background) {
    Require(observation.lifetimeAdmitted && observation.colorsAdmitted && observation.colors,
        "A recovered owned footer must carry independent lifetime and color authority");
    Require(Same(observation.colors->outline, outline) && Same(observation.colors->background, background),
        "Graphics preparation lost an actual owner observation or changed the literal RGB oracle");
}
void Dump(std::ostream* stream, const char* scenario, bool preview, const char* packet,
    std::uint64_t nativeClock, const InstructionPaintObservation& observation) {
    if (!stream) return;
    *stream << scenario << ',' << (preview ? "preview" : "runtime") << ',' << packet << ',' << nativeClock
        << ',' << observation.lifetimeAdmitted << ',' << observation.colorsAdmitted;
    for (auto color : {observation.colors ? observation.colors->outline : layout::Color{},
            observation.colors ? observation.colors->background : layout::Color{}}) {
        *stream << ',' << unsigned(color.red) << ',' << unsigned(color.green) << ',' << unsigned(color.blue) << ',' << unsigned(color.alpha);
    }
    *stream << '\n';
}
}
void ActualFooterCreationSamplesSurviveFailedAndThrowingGraphicsPreparation(
    const std::filesystem::path& directory, std::ostream* dump) {
    footer_withdrawal::CheckInventory(directory);
    for (bool preview : {false, true}) {
        for (bool throwing : {false, true}) {
            InstructionHudLifetime lifetime;
            InstructionHudColorHistory history;
            const auto birth = Read(directory, Expected("footer-withdrawal-rainbow-created.json"), preview);
            const auto original = birth;
            unsigned painted = 0;
            bool threw = false;
            try {
                // Failure belongs to graphics setup, after the native owner has observed this exact Rainbow birth
                RunInstructionRenderPhase(birth, lifetime, history, SampleOracles[0].observed, 1610,
                    [throwing](bool admitted) {
                        Require(admitted, "Actual Rainbow birth must admit its known local owner");
                        if (throwing) throw std::runtime_error("Owned preparation fault");
                        return false;
                    }, [&](const InstructionPaintObservation&) { ++painted; return true; });
            } catch (const std::runtime_error& exception) {
                Require(throwing && std::string(exception.what()) == "Owned preparation fault", "Unexpected source or phase failure");
                threw = true;
            }
            Require(threw == throwing && painted == 0, "Failed preparation cannot execute the paint callback");
            Unchanged(original, birth);
            const auto fixed = Read(directory, Expected("footer-withdrawal-rainbow-fixed180.json"), preview);
            // Recovery uses a different sample; the already observed creation must not be sampled again
            RunInstructionRenderPhase(fixed, lifetime, history, layout::Color{20,40,60,255}, 1790,
                [](bool admitted) { return admitted; }, [&](const InstructionPaintObservation& observation) {
                    Check(observation, SampleOracles[0].fixed180, {42,42,42,255});
                    Dump(dump, throwing ? "birth-throw" : "birth-false", preview, "fixed180", 1790, observation);
                    ++painted;
                    return true;
                });
            Require(painted == 1, "Known recovery must paint exactly once");
            const auto restored = Read(directory, Expected("footer-withdrawal-rainbow-restored430.json"), preview);
            RunInstructionRenderPhase(restored, lifetime, history, layout::Color{9,8,7,255}, 2040,
                [](bool admitted) { return admitted; }, [&](const InstructionPaintObservation& observation) {
                    Check(observation, SampleOracles[0].fixed430, {42,42,42,255});
                    Dump(dump, throwing ? "birth-throw" : "birth-false", preview, "fixed430", 2040, observation);
                    return true;
                });
        }
        InstructionHudLifetime lifetime;
        InstructionHudColorHistory history;
        const auto birth = Read(directory, Expected("footer-withdrawal-rainbow-created.json"), preview);
        RunInstructionRenderPhase(birth, lifetime, history, SampleOracles[0].observed, 1610,
            [](bool admitted) { return admitted; }, [](const InstructionPaintObservation&) { return true; });
        unsigned painted = 0;
        // A repeated current report samples its existing Rainbow widget even if graphics resources are unavailable
        RunInstructionRenderPhase(birth, lifetime, history, SampleOracles[1].observed, 1650,
            [](bool admitted) { Require(admitted, "Repeated birth remains owned"); return false; },
            [&](const InstructionPaintObservation&) { ++painted; return true; });
        Require(painted == 0, "Unavailable resources cannot paint the sampled Rainbow widget");
        const auto fixed = Read(directory, Expected("footer-withdrawal-rainbow-fixed180.json"), preview);
        RunInstructionRenderPhase(fixed, lifetime, history, layout::Color{20,40,60,255}, 1790,
            [](bool admitted) { return admitted; }, [&](const InstructionPaintObservation& observation) {
                Check(observation, SampleOracles[1].fixed180, {42,42,42,255});
                Dump(dump, "repeat-false", preview, "fixed180", 1790, observation);
                return true;
            });
        const auto restored = Read(directory, Expected("footer-withdrawal-rainbow-restored430.json"), preview);
        RunInstructionRenderPhase(restored, lifetime, history, layout::Color{1,2,3,255}, 2040,
            [](bool admitted) { return admitted; }, [&](const InstructionPaintObservation& observation) {
                Check(observation, SampleOracles[1].fixed430, {42,42,42,255});
                Dump(dump, "repeat-false", preview, "fixed430", 2040, observation);
                return true;
            });
    }
}
void ActualFooterChronologySeparatesColorObservationFromPaintAndGameAuthority(
    const std::filesystem::path& directory, std::ostream* dump) {
    footer_withdrawal::CheckInventory(directory);
    for (bool preview : {false, true}) {
        unsigned graph = 99;
        std::uint64_t nativeClock = 0;
        std::unique_ptr<InstructionHudLifetime> lifetime;
        std::unique_ptr<InstructionHudColorHistory> history;
        for (const auto& expected : footer_withdrawal::Profiles) {
            if (graph != expected.graph) {
                graph = expected.graph;
                nativeClock = 0;
                lifetime = std::make_unique<InstructionHudLifetime>();
                history = std::make_unique<InstructionHudColorHistory>();
            }
            const auto state = Read(directory, expected, preview);
            const auto original = state;
            // Backend rollback in a packet never fabricates a rollback of the separate native owner clock
            nativeClock = std::max(nativeClock, expected.clock);
            const bool fault = std::string(expected.name) == "footer-withdrawal-open-known.json"
                || std::string(expected.name) == "footer-withdrawal-rainbow-created.json"
                || std::string(expected.name) == "footer-withdrawal-rainbow-fixed180.json";
            unsigned painted = 0;
            // A stale Rainbow value must not update widgets while current life proof is withdrawn
            const auto sample = expected.creation ? SampleOracles[0].observed : layout::Color{0,255,0,255};
            RunInstructionRenderPhase(state, *lifetime, *history, sample, nativeClock,
                [&](bool admitted) {
                    Require(admitted == (expected.creation != 0), "Graphics failure cannot manufacture local lifetime admission");
                    return !fault;
                }, [&](const InstructionPaintObservation& observation) {
                    ++painted;
                    Require(observation.lifetimeAdmitted == (expected.creation != 0)
                        && observation.colorsAdmitted == observation.colors.has_value(), "Paint authority must stay separate from resource readiness");
                    footer_withdrawal::CheckColors(expected, observation.colors);
                    Dump(dump, "chronology", preview, expected.name, nativeClock, observation);
                    return true;
                });
            Require(painted == (fault ? 0U : 1U), "Fault scheduling must change painting only");
            Unchanged(original, state);
        }
    }
}
}
