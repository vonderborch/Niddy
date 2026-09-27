using System.ComponentModel;
using Avalonia.Input;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     An item in a <see cref="PageMenu" />: a page at one place in the menu's tree. A page with several parents has
///     an item under each. Bind an <c>ItemTemplate</c> to its properties to customize the menu.
/// </summary>
public sealed class PageMenuItem : INotifyPropertyChanged
{
    private readonly Action<PageMenuItem> _expandedChanged;

    private bool _isExpanded;

    /// <summary>The page's registration.</summary>
    public PageRegistration Registration { get; }

    /// <summary>The page's ID.</summary>
    public PageId PageId => Registration.PageId;

    /// <summary>The page's class.</summary>
    public Type PageType => Registration.PageType;

    /// <summary>The page's display name.</summary>
    public string DisplayName => Registration.DisplayName;

    /// <summary>The page's icon, or null if it has none.</summary>
    public PageIcon? Icon => Registration.Icon;

    /// <summary>The page's keyboard shortcut, or null. See <see cref="Shortcut" />.</summary>
    public KeyGesture? Shortcut => Registration.Shortcut;

    /// <summary>The item this one is nested under, or null at the top of the menu.</summary>
    public PageMenuItem? Parent { get; }

    /// <summary>How deeply the item is nested: 0 at the top of the menu.</summary>
    public int Depth { get; }

    /// <summary>The items nested under this one. Empty in a <see cref="Flat" /> menu.</summary>
    public IReadOnlyList<PageMenuItem> Children { get; internal set; } = Array.Empty<PageMenuItem>();

    /// <summary>Whether items are nested under this one.</summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>Whether the nested items are shown. The menu expands the items leading to the current page.</summary>
    public bool IsExpanded
    {
        get
        {
            return _isExpanded;
        }
        set
        {
            if (SetExpanded(value))
            {
                _expandedChanged(this);
            }
        }
    }

    /// <summary>The page this item's page is a child of here: the parent item's page, or the menu's parent page.</summary>
    internal PageId? ParentPage { get; }

    /// <summary>Identifies the item's place in the tree, to keep it expanded when the menu is rebuilt.</summary>
    internal string Key { get; }

    /// <summary>The items from the top of the menu down to this one.</summary>
    internal IReadOnlyList<PageMenuItem> Path
    {
        get
        {
            List<PageMenuItem> list = new List<PageMenuItem>();
            for (PageMenuItem pageMenuItem = this; pageMenuItem != null; pageMenuItem = pageMenuItem.Parent)
            {
                list.Insert(0, pageMenuItem);
            }
            return list;
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    internal PageMenuItem(PageRegistration registration, PageMenuItem? parent, PageId? parentPage, Action<PageMenuItem> expandedChanged)
    {
        Registration = registration;
        Parent = parent;
        ParentPage = parentPage;
        Depth = ((parent != null) ? (parent.Depth + 1) : 0);
        Key = parent?.Key + "/" + registration.PageType.FullName;
        _expandedChanged = expandedChanged;
    }

    /// <summary>Changes <see cref="IsExpanded" /> without telling the menu; returns whether it changed.</summary>
    internal bool SetExpanded(bool value)
    {
        if (_isExpanded == value)
        {
            return false;
        }
        _isExpanded = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return DisplayName;
    }
}
