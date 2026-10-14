#pragma once
#include "NativeInputRouter.h"
#include <set>
#include <stdexcept>

namespace iw::overlay::runtime::tests {
inline void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
struct UseFollowupRig {
    Bindings bindings{{2}, {86}, {2,38}, {1,40}, {70}, {51}, {52}, {1}, {38}, {40}, {71}, {69}, {16}, {17}, {32}};
    NativeInputRouter router{bindings};
    std::set<int> held;
    bool focused=true, open=true, pending=false, quick=false, root=false;
    std::uint64_t now=0, navigation=7, menu=20, ack=10;
    InputMode mode=InputMode::Menu;
    LocalMenuEligibility eligibility{4,PlayerLifeState::Alive,true,false,false};
    MenuSurface surface=MenuSurface::Main;
    std::optional<MenuUseContinuation> continuation;
    std::optional<MenuOptionCallback> callback;
    std::optional<Input> Poll() {
        // A unit sample keeps receipt, callback and acknowledgment in one observation
        UseMonitorObservation observation{continuation ? &*continuation : nullptr,callback ? &*callback : nullptr,ack};
        return router.Poll(focused,open,pending,[&](int key){return held.contains(key);},now,
            navigation,quick,root,mode,120,menu,nullptr,0,0,nullptr,&eligibility,surface,nullptr,&observation);
    }
    void Admit(MenuOptionCallbackKind kind=MenuOptionCallbackKind::Plain, bool capture=true) {
        Poll();held={70};now=1;Require(Poll()==Input::Select,"Original Select was not admitted");
        // Missing capture represents a failed write rather than a later successful command
        if(capture)router.CaptureUseDispatch(11);
        ack=11;continuation=MenuUseContinuation{1,11,4,kind};
        callback=MenuOptionCallback{1,kind,kind==MenuOptionCallbackKind::Plain
            ? MenuOptionCallbackPhase::Cooldown : MenuOptionCallbackPhase::Pending};
        mode=InputMode::MenuLocked;eligibility.controlsDisabled=true;now=2;
        Require(!Poll(),"Original callback did not pause the monitor");
    }
    void Hold(std::set<int> keys) {
        // Sampling during the callback consumes a physical edge before readiness
        held=std::move(keys);now=100;
        Require(!Poll(),"A lower held branch bypassed the callback");
    }
    void Ready() {
        // The same retained receipt proves normal retirement without a native deadline
        callback.reset();mode=InputMode::Menu;eligibility.controlsDisabled=false;now=500;
    }
};
}
