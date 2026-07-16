namespace TristansTrackers;

internal sealed class HudMenuManager
{
    private HudMenuWindow? _openMenu;

    public void Show(IEnumerable<HudMenuItem> items, HudMenuAnchor anchor)
    {
        Close();

        var menu = new HudMenuWindow(items);
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openMenu, menu))
            {
                _openMenu = null;
            }
        };

        _openMenu = menu;
        menu.ShowAt(anchor);
    }

    public void EnsureTopmost()
    {
        _openMenu?.EnsureTopmost();
    }

    public void Close()
    {
        HudMenuWindow? menu = _openMenu;
        _openMenu = null;
        menu?.Close();
    }
}
