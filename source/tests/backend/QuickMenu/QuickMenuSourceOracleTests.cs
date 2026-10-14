using System.Text.Json;
using Apparition.Backend.QuickMenu;

namespace Backend.Tests;

// The pinned declaration fixture comes from the source argument order, not catalogue output
internal static class QuickMenuSourceOracleTests {
    public static void CatalogMatchesPinnedSourceLabelsChoicesAndConditionalMembership() {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "QuickMenu", "source-oracle.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(fixture));
        var source = document.RootElement;
        TestAssert.Equal("3e8ee8a24c25f74fe45d146f5b52ae466afdb120e0987e97c90a78c36720f2b5",
            source.GetProperty("sourceSha256").GetString());
        string[] labels = source.GetProperty("labelsInSourceOrder").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        TestAssert.Equal(14, labels.Length);
        foreach (var life in new[] { QuickPlayerLife.Living, QuickPlayerLife.Dead, QuickPlayerLife.Unknown }) {
            foreach (bool? host in new bool?[] { null, false, true }) {
                foreach (bool? developer in new bool?[] { null, false, true }) {
                    var facts = new QuickMenuEligibility(life, host, developer);
                    var catalog = QuickMenuCatalog.Build(facts);
                    var expected = life == QuickPlayerLife.Living
                        ? Enumerable.Range(0, 12).ToList() : new List<int> { 10 };
                    // Known authority adds only the final two source declarations
                    if (host == true || developer == true) expected.AddRange(new[] { 12, 13 });
                    TestAssert.Equal(expected.Count, catalog.Count);
                    for (int row = 0; row < catalog.Count; row++) {
                        var item = catalog[row];
                        TestAssert.Equal(expected[row], (int)item.Id);
                        TestAssert.Equal(labels[expected[row]], item.Label);
                        TestAssert.True(!item.IsAvailable && !item.CanDispatch,
                            "A declaration cannot grant an unbound game callback");
                        if (source.GetProperty("choices").TryGetProperty(item.Label, out var choices)) {
                            string[] values = choices.EnumerateArray().Select(choice => choice.GetString()!).ToArray();
                            TestAssert.True(values.SequenceEqual(item.Choices), "Source slider order changed");
                        } else {
                            TestAssert.Equal(0, item.Choices.Count);
                        }
                    }
                }
            }
        }
    }
}
