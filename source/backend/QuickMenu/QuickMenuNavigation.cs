namespace Apparition.Backend.QuickMenu;

// The separate path owner does not replace the current production navigation
public enum QuickMenuSurface { Main, Quick }
public sealed class QuickMenuNavigation {
    private const int MaximumRows = 128;
    private readonly string[] mainParents;
    private readonly string[] quickParents;
    private readonly Dictionary<string, int> cursors;
    private readonly Dictionary<(string Page, int Row), int> drafts;
    public QuickMenuSurface? ActiveSurface { get; }
    public string MainPage { get; }
    public string QuickPage { get; }
    public string? CurrentPage => ActiveSurface switch { QuickMenuSurface.Main => MainPage, QuickMenuSurface.Quick => QuickPage, _ => null };
    public int CurrentCursor => CurrentPage is { } page ? GetCursor(page) : 0;

    public QuickMenuNavigation() : this(null, "Main", "Quick Menu", [], [], new(StringComparer.Ordinal), new()) { }
    private QuickMenuNavigation(QuickMenuSurface? surface, string main, string quick, string[] mainParents,
        string[] quickParents, Dictionary<string, int> cursors, Dictionary<(string Page, int Row), int> drafts) {
        ActiveSurface = surface;
        MainPage = main;
        QuickPage = quick;
        this.mainParents = mainParents;
        this.quickParents = quickParents;
        this.cursors = cursors;
        this.drafts = drafts;
    }

    private QuickMenuNavigation Copy(QuickMenuSurface? surface, string? main = null, string? quick = null,
        string[]? mainPath = null, string[]? quickPath = null, Dictionary<string, int>? cursorMap = null,
        Dictionary<(string Page, int Row), int>? draftMap = null) =>
        new(surface, main ?? MainPage, quick ?? QuickPage, mainPath ?? mainParents, quickPath ?? quickParents,
            cursorMap ?? cursors, draftMap ?? drafts);

    public QuickMenuNavigation Open(QuickMenuSurface surface) {
        ValidateSurface(surface);
        if (ActiveSurface == surface) return this;
        if (ActiveSurface is not null) throw new InvalidOperationException("A menu family is already open");
        // Reopening preserves each family's current page and page-name parent path
        return Copy(surface);
    }
    public QuickMenuNavigation Close() => ActiveSurface is null ? this : Copy(null);

    public QuickMenuNavigation Visit(string page) {
        string current = RequireOpen();
        ValidatePage(page);
        var path = ActiveSurface == QuickMenuSurface.Main ? mainParents : quickParents;
        if (path.Length == MaximumRows) throw new InvalidOperationException("The local parent path is full");
        var next = new string[path.Length + 1];
        Array.Copy(path, next, path.Length);
        // Source parents store only page names; cursor state is shared separately
        next[^1] = current;
        return ActiveSurface == QuickMenuSurface.Main ? Copy(ActiveSurface, main: page, mainPath: next)
            : Copy(ActiveSurface, quick: page, quickPath: next);
    }

    public QuickMenuNavigation Back() {
        RequireOpen();
        var path = ActiveSurface == QuickMenuSurface.Main ? mainParents : quickParents;
        if (path.Length == 0) return Close();
        // Parent entries never restore an obsolete cursor captured before the other family visited
        var next = path[..^1];
        return ActiveSurface == QuickMenuSurface.Main ? Copy(ActiveSurface, main: path[^1], mainPath: next)
            : Copy(ActiveSurface, quick: path[^1], quickPath: next);
    }

    public QuickMenuNavigation WithCursor(int index) {
        string page = RequireOpen();
        ValidateRow(index);
        // Copy only the changed map so every earlier navigation snapshot remains unchanged
        var next = new Dictionary<string, int>(cursors, StringComparer.Ordinal) { [page] = index };
        return Copy(ActiveSurface, cursorMap: next);
    }

    public QuickMenuNavigation WithChoiceDraft(int row, int candidate, int choiceCount) {
        string page = RequireOpen();
        ValidateRow(row);
        if (choiceCount is < 1 or > MaximumRows) throw new ArgumentOutOfRangeException(nameof(choiceCount));
        if (candidate < 0 || candidate >= choiceCount) throw new ArgumentOutOfRangeException(nameof(candidate));
        var next = new Dictionary<(string Page, int Row), int>(drafts) { [(page, row)] = candidate };
        return Copy(ActiveSurface, draftMap: next);
    }

    public int GetCursor(string page) {
        ValidatePage(page);
        return cursors.GetValueOrDefault(page);
    }
    public int? GetChoiceDraft(string page, int row) {
        ValidatePage(page);
        ValidateRow(row);
        // Missing source slider state stays undefined until a declared slider supplies its default
        return drafts.TryGetValue((page, row), out int candidate) ? candidate : null;
    }
    public IReadOnlyList<string> Parents(QuickMenuSurface surface) {
        ValidateSurface(surface);
        return Array.AsReadOnly(surface == QuickMenuSurface.Main ? mainParents : quickParents);
    }

    private string RequireOpen() => CurrentPage ?? throw new InvalidOperationException("No menu family is open");
    private static void ValidateSurface(QuickMenuSurface surface) {
        if (!Enum.IsDefined(surface)) throw new ArgumentOutOfRangeException(nameof(surface));
    }
    private static void ValidatePage(string page) {
        // Page keys remain exact source names rather than rewritten display labels
        if (string.IsNullOrWhiteSpace(page) || page.Length > 256)
            throw new ArgumentException("A bounded source page name is required", nameof(page));
    }
    private static void ValidateRow(int index) {
        if (index is < 0 or >= MaximumRows) throw new ArgumentOutOfRangeException(nameof(index));
    }
}
