using System.Text.Json.Serialization;

namespace syndishanx_iw_tool.Overlay;

// The owner publishes the source design rebuild and independent monitor cooldown
public sealed record MenuDesignTransition(string Phase,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FooterDesign = null)
{
    public bool IsSupported() => (Phase is "rebuild" or "cooldown")
        && (FooterDesign is null || Phase == "rebuild" && MenuDesignDraft.IsSupportedDesign(FooterDesign));

    public bool IsSupported(MenuAppearance appearance)
    {
        System.ArgumentNullException.ThrowIfNull(appearance);
        // Placement uses the committed target while existing footer paint remains captured
        return IsSupported() && appearance.IsSupported() && (FooterDesign is null
            || (MenuDesignDraft.AllowsColoredCursor(FooterDesign) || !appearance.ColoredCursor)
                && (MenuDesignDraft.AllowsOptionCounter(FooterDesign) || !appearance.OptionCounter));
    }
}
