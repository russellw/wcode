using System.Windows.Controls;
using System.Windows;

namespace wcode;

public enum TabType
{
    File,
    Directory,
    Terminal
}

public class TabInfo
{
    public TabType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Path { get; set; }
    public FrameworkElement Content { get; set; } = null!;
    public string Icon { get; set; } = string.Empty;
}