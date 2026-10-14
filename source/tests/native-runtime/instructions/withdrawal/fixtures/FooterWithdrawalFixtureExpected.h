#pragma once
#include "../../../../../native/shared/MenuSnapshot.h"
#include "../../../../../native/render/instructions/color/InstructionHudColors.h"
#include <array>
namespace iw::overlay::tests::footer_withdrawal {
struct Expected {
    const char* name;unsigned graph;std::uint64_t clock,creation,admission,navigation,menu,sequence;
    bool open;PlayerLifeState life;MenuDesign design;bool alternate,rainbow;unsigned events;
    bool prefix;std::uint64_t latestAge;InstructionHudColors colors;
};
// Literal RGB comes from source creation colors and the declared native linear adapter
inline constexpr std::array<Expected,46> Profiles{{
    {"footer-withdrawal-open-known.json",0,930,1,10,14,7,13,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,180,{{128,97,163,255},{42,42,42,255}}},
    {"footer-withdrawal-open-unknown.json",0,930,0,11,14,7,13,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-open-restored.json",0,1180,1,12,14,7,13,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,430,{{227,21,36,255},{42,42,42,255}}},
    {"footer-withdrawal-closed-known.json",1,1180,1,10,15,8,14,false,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,430,{{227,21,36,255},{42,42,42,255}}},
    {"footer-withdrawal-closed-unknown.json",1,1180,0,11,15,8,14,false,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-closed-restored.json",1,1430,1,12,15,8,14,false,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,680,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-life-alive.json",2,930,1,10,14,7,13,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,180,{{128,97,163,255},{42,42,42,255}}},
    {"footer-withdrawal-life-unknown-alive.json",2,930,0,11,14,7,13,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-life-dead.json",2,930,2,12,14,7,13,true,PlayerLifeState::Dead,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-life-unknown-dead.json",2,930,0,13,14,7,13,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-life-dead-again.json",2,930,2,14,14,7,13,true,PlayerLifeState::Dead,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-life-revived.json",2,930,2,15,14,7,13,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-delayed-started.json",3,1180,1,10,19,11,18,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,430,{{227,21,36,255},{42,42,42,255}}},
    {"footer-withdrawal-delayed-unknown25.json",3,1205,0,11,19,11,18,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-delayed-restored30.json",3,1210,1,12,19,11,18,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,460,{{239,12,20,255},{42,42,42,255}}},
    {"footer-withdrawal-delayed-drawn50.json",3,1230,2,12,19,12,18,true,PlayerLifeState::Alive,MenuDesign::Classic,false,false,0,true,0,{{255,0,0,255},{25,25,25,217}}},
    {"footer-withdrawal-ready-started.json",4,1180,1,10,19,11,18,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,430,{{227,21,36,255},{42,42,42,255}}},
    {"footer-withdrawal-ready-unknown25.json",4,1205,0,11,19,11,18,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-ready-drawn-unknown50.json",4,1230,0,11,19,12,18,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-ready-restored60.json",4,1240,2,12,19,12,18,true,PlayerLifeState::Alive,MenuDesign::Classic,false,false,0,true,0,{{255,0,0,255},{25,25,25,217}}},
    {"footer-withdrawal-permission-unknown.json",5,930,0,11,14,7,13,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-permission-revoked.json",5,930,0,12,15,8,13,false,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-permission-restored.json",5,930,2,13,15,8,13,false,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-provider-unknown.json",6,930,0,11,14,7,13,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-provider-revoked.json",6,930,0,0,15,8,13,false,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-provider-restored.json",6,930,2,12,15,8,13,false,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-owner-unknown.json",7,930,0,11,14,7,13,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-owner-revoked.json",7,930,0,12,15,8,13,false,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-owner-restored.json",7,930,2,13,15,8,13,false,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-process-unknown.json",8,930,0,11,14,7,13,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-process-revoked.json",8,930,0,0,15,8,13,false,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-process-restored.json",8,930,2,12,15,8,13,false,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-disable-unknown.json",9,1180,0,11,24,11,21,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-disable-restored.json",9,1180,1,12,24,11,21,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,1,true,430,{{227,21,36,255},{42,42,42,255}}},
    {"footer-withdrawal-disable-disabled.json",9,1180,0,12,24,12,22,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-disable-enabled.json",9,1610,2,12,24,14,23,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,true,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-prefix-pruned.json",10,4620,1,10,14,15,21,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,8,false,430,{{57,152,254,255},{42,42,42,255}}},
    {"footer-withdrawal-prefix-unknown.json",10,4620,0,11,14,15,21,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-prefix-restored.json",10,4720,1,12,14,15,21,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,8,false,530,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-prefix-unknown-again.json",10,4720,0,13,14,15,21,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-prefix-rollback-unknown.json",10,4570,0,13,14,15,21,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-prefix-recovered-timing.json",10,4570,1,14,14,15,21,true,PlayerLifeState::Alive,MenuDesign::Apparition,false,false,0,false,0,{{255,0,0,255},{42,42,42,255}}},
    {"footer-withdrawal-rainbow-created.json",11,1610,2,10,61,12,59,true,PlayerLifeState::Alive,MenuDesign::Apparition,true,true,0,true,0,{{80,160,240,255},{42,42,42,255}}},
    {"footer-withdrawal-rainbow-fixed180.json",11,1790,2,10,67,17,64,true,PlayerLifeState::Alive,MenuDesign::Apparition,true,true,1,true,180,{{143,102,154,255},{42,42,42,255}}},
    {"footer-withdrawal-rainbow-unknown.json",11,1790,0,11,67,17,64,true,PlayerLifeState::Unknown,MenuDesign::Apparition,false,false,0,false,0,{{0,0,0,255},{0,0,0,255}}},
    {"footer-withdrawal-rainbow-restored430.json",11,2040,2,12,67,17,64,true,PlayerLifeState::Alive,MenuDesign::Apparition,true,true,1,true,430,{{231,22,34,255},{42,42,42,255}}},
}};
}
