# Personal restoration feedback

The explicit personal restore adapter discarded the registry operation result and logged only the local view status. A failed operation can leave the view unchanged, so Messages could end with `Restore your Godmode: On` and omit the failure reason. A rejected native restore also omitted the UNKNOWN value from its history entry.

The session now retains the result of the existing `SetAsync(identity, false)` call. Success keeps the existing status message. Failure reports the observed ON/OFF/UNKNOWN value and the result's failure reason. A missing reason receives the explicit fallback `Restoration was not confirmed`. Cached personal status continues describing the observed local view; it is not repurposed as operation history or permission.

The two existing real-session history regressions now exercise both Toggle and Restore requests using the same owned flag fixtures. They cover confirmed ON/OFF, a rejected OFF leaving UNKNOWN, and an identity-query failure before local mutation leaving ON. Final words, write counts, original binding count and absence of health/ammo writes are checked before comparing messages. Restoration cleanup and shutdown remain under the original owners. Restoring only the old logging implementation reintroduces both failed Restore cases.

No gameplay write, admission, offset, target, callback clock, native protocol or renderer behavior changes. This is an IW cleanup adapter for unconfirmed personal protection; the reference has no matching recovery row. Accurate messages improve the adaptation without claiming that these failures prove the earlier retail Godmode issue. Offline session tests do not prove damage coverage, co-op toggles, all-map support or full Apparition parity.
