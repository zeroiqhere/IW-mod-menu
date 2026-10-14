#include "BooleanPaintTests.h"
#include "../scroll/SpacerScrollTestModels.h"

namespace iw::overlay::tests::paint {
void PaintIdentityRetiresPrivateScrollReceipts() {
    auto source = spacer::Source(); MenuBooleanPaint paint; paint.revision = 7; paint.count = 1;
    paint.rows[0] = {0, MenuBooleanPaintValue::On}; source.menuBooleanPaint = paint;
    for (int change = 0; change < 3; ++change) {
        runtime::scroll::SpacerScrollReceiptTracker tracker;
        Check(tracker.Capture(source, SpacerScrollDirection::Down, 12, 100), "Valid painted source could not capture a spacer dispatch");
        auto target = spacer::Target(source, 3);
        auto confirmed = tracker.Observe(target, 12, 120);
        Check(confirmed && confirmed->sourceMenuBooleanPaint == source.menuBooleanPaint, "Private receipt discarded copied paint identity");
        if (change == 0) ++target.menuBooleanPaint->revision;
        if (change == 1) target.menuBooleanPaint->rows[0].value = MenuBooleanPaintValue::Off;
        if (change == 2) target.menuBooleanPaint.reset();
        Check(!tracker.Observe(target, 12, 130) && !tracker.Observe(spacer::Target(source, 3), 12, 140),
            "Changed paint retained or revived private scroll history");
    }
}
}
