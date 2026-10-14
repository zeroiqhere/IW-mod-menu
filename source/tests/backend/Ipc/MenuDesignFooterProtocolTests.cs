using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuDesignFooterProtocolTests
{
    public static void FooterDescriptorAcceptsOnlyExactRebuildTargetsAndTheirCommittedFlagNormalization()
    {
        var menu = new MenuSnapshot("Menu Preferences", "PREVIEW", new[] { new MenuRow("Design", "AIO", true, new(MenuActionKind.None)) }, "");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        StateDocument Create(string phase, string? footer, MenuAppearance? appearance = null)
        {
            string json = JsonSerializer.Serialize(new
            {
                phase,
                footerDesign = footer
            });
            var transition = JsonSerializer.Deserialize<MenuDesignTransition>(json, options)!;
            return StateDocument.Create(42, 0, true, menu, 0, appearance: appearance, isRootMenu: false, menuDesignTransition: transition);
        }
        foreach (string target in MenuDesignTests.Names)
        {
            // Captured paint can differ, but committed flags must already satisfy the target domain
            var normalized = MenuAppearance.Default with
            {
                ColoredCursor = MenuDesignDraft.AllowsColoredCursor(target),
                OptionCounter = false
            };
            var state = Create("rebuild", target, normalized);
            TestAssert.Equal(target, MenuDesignFooterTests.FooterDesign(state));
        }
        foreach (string invalid in new[] { "", "aio", "Aio", "classic", "Unknown" })
        {
            TestAssert.Throws<InvalidOperationException>(() => Create("rebuild", invalid));
        }
        foreach (string target in MenuDesignTests.Names)
        {
            TestAssert.Throws<InvalidOperationException>(() => Create("cooldown", target));
        }
        foreach (string target in new[] { "Classic", "Native" })
        {
            TestAssert.Throws<InvalidOperationException>(() => Create("rebuild", target, MenuAppearance.Default));
        }
        foreach (string target in new[] { "AIO", "Basic" })
        {
            TestAssert.Throws<InvalidOperationException>(() => Create("rebuild", target, MenuAppearance.Default with { OptionCounter = true }));
        }
        TestAssert.True(!new MenuDesignTransition("rebuild", "Apparition").IsSupported(MenuAppearance.Default with
        {
            Design = "Classic"
        }),
            "The public descriptor boundary also rejects unsupported captured appearance");
        foreach (string malformed in new[] { "true", "12", "[]", "{}" })
        {
            TestAssert.Throws<JsonException>(() => JsonSerializer.Deserialize<MenuDesignTransition>("{\"phase\":\"rebuild\",\"footerDesign\":" + malformed + "}", options));
        }
        foreach (string phase in new[] { "rebuild", "cooldown" })
        {
            var legacy = Create(phase, null);
            using var json = JsonDocument.Parse(legacy.Serialize());
            TestAssert.True(!json.RootElement.GetProperty("menuDesignTransition").TryGetProperty("footerDesign", out _),
                "Null legacy descriptors are omitted rather than changing the existing phase-only packet");
        }
    }
}
