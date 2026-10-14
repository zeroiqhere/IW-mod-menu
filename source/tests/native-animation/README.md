# Width presentation animation

This portable executable compiles production `WidthAnimation.cpp` with an explicit
monotonic clock. It starts no window, GPU, hook, backend or game process.

```sh
g++ -std=c++20 -Wall -Wextra -Werror \
  tests/native-animation/WidthAnimationTests.cpp native/render/WidthAnimation.cpp \
  -o /tmp/iw-width-animation-tests
/tmp/iw-width-animation-tests
```

The Windows CMake target is `iw_width_animation_tests`. The renderer agent confirms
eight checks: initial values, 50 ms boundaries, reversal from current displayed
width, context changes, close/reset, rollback/large clocks and bounded input.
The renderer agent confirms its Windows cross-build and sixteen actual CPU
preview comparisons. Root confirms the integrated main native build and the same sixteen fresh
producer snapshots through parser/CPU comparisons; the full source wave totals 441.

Committed width is immutable menu state; displayed width is render-thread local
presentation state. Confirmed changes within one open editor context interpolate
linearly over 50 ms. Initial entry, changed navigation identity and leaving the
editor snap to the committed value, preventing movement from leaking into another
page. Retargeting begins at the current displayed edge, not the previous target.
No game state or committed preference is mutated by animation.

The pinned Apparition helper requests `ScaleOverTime(0.05,width,height)` and sets
its script width/height fields immediately, but supplies no easing equation.
Linear interpolation is an explicit native approximation; these checks prove
native duration and state behavior, not BO3 engine easing or retail GPU cadence.


The production renderer uses `GetTickCount64` on its owner thread. [Microsoft's
current API documentation](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64)
reports typical timer resolution of 10..16 ms. Explicit test-clock boundaries do
not prove that resolution under Proton or promise a visible frame at exactly 50 ms.
