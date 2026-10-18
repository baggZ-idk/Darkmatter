#region usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using DiscordRPC;
using Newtonsoft.Json;
using Button = Avalonia.Controls.Button;
#endregion

namespace Darkmatter;

public partial class MainWindow : Window
{
    #region Properties
    public List<Mod> Mods { get; } = new();
    private string gamePath;
    private string pluginsPath;
    private readonly HttpClient httpClient = new();
    Theme CurrentTheme;
    private DiscordRpcClient client;
    private List<ModBorderThingyForDictionary> modBorders = new();
    public static MainWindow Instance;
    List<TextBlock> textBlocks = new();
    List<TextBlock> secondaryTextBlocks = new();
    List<TextBlock> GroupTextBlocks = new();
    List<TextBlock> OtherGroupTextBlocks = new();
    private static List<Border> ModControls = new();
    private NotificationManager notificationManager = new();
    private Border badgeBorder;
    private TextBlock badgeText;
    private Popup notificationPopup;
    #endregion
    
    #region Window And Init
    public MainWindow()
    {
        Instance = this;
        DataContext = this;
        InitializeComponent();
        
        this.Opened += async (s, e) =>
        {
            try
            {
                Opacity = 0;
                ShowStartWindow();
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"Initialization failed: {ex.Message}");
            }
        };
    }

    bool ModsDisabled()
    {
        if (File.Exists(Path.Combine(gamePath, "winhttp.dll")))
        {
            DisableEnable.Header = "Disable Mods";
            return false;
        }
        DisableEnable.Header = "Enable Mods";
        return true;
    }

    private async Task InitializeAsync()
    {
        if (!File.Exists(GetConfigPath()))
        {
            var selectedPath = await ShowGamePathDialog();
            if (string.IsNullOrEmpty(selectedPath))
            {
                Close();
                return;
            }
            await SaveConfig(selectedPath, GetTheme());
        }
        else
        {
            gamePath = GetGamePath();
            if (string.IsNullOrEmpty(gamePath))
            {
                await ShowErrorMessage("select a game path");
                var selectedPath = await ShowGamePathDialog();
                if (string.IsNullOrEmpty(selectedPath))
                {
                    Close();
                    return;
                }

                await SaveConfig(selectedPath, GetTheme());
            }
        }
        pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");

        var (updateAvailable, newVersion) = await IsUpdateAvailable(new Version(1, 0, 1));

        if (updateAvailable)
        {
            await NewVersionDialog(newVersion);
        }
        CurrentTheme = GetTheme();
        InitUI();
        
        await CheckOrInstallBepInEx();
        await LoadModsFromTheNewGitHubRepoAsync();
        MakeNotificationThing();
        SendNeededMesages();
        MenuImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Darkmatter/Assets/menu-google.png")));
    }

    void SendNeededMesages()
    {
        if (ModsDisabled())
        {
            notificationManager.ShowNotification("Mods Disabled!", "The mods will not function!");
        }
    }
    void InitUI()
    {
        TitleText.Foreground = getTextTheme();
        MessageBox0.Foreground = getTextTheme();
        MainGrid.Background = getBGForTheme();
        ModsThingy.Background = getBGForTheme();
        MsgBox0Border.Background = GetCardBGS();
        ModsThingy.BorderBrush = GetBorderBrush();
        SearchBox.Foreground = getTextTheme();
        SearchBox.Background = GetCardBGS();
        SortBox.Foreground = getTextTheme();
        SortBox.Background = GetCardBGS();
    }


    private Window window;

    private void ShowStartWindow()
    {
        var stackPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var img = new Image
        {
            Width = 150,
            Height = 150,
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://Darkmatter/Assets/mmm-ico.png"))),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        stackPanel.Children.Add(img);

        window = new Window
        {
            Content = stackPanel,
            Width = 300,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
            ShowInTaskbar = false,
            Background = getBGForTheme(),
            SystemDecorations = SystemDecorations.None,
            SizeToContent = SizeToContent.Manual
        };
        window.Show();
    }

    void closeStartWindow()
    {
        window.Close();
    }
    
    public IBrush getBGForTheme()
    {
        return GetTheme() switch
        {
            Darkmatter.Theme.Light => Brush.Parse("#FFFFFF"),
            Darkmatter.Theme.Dark => Brush.Parse("#121212"),
            Darkmatter.Theme.DarkHighContrast => Brush.Parse("#000000"),
            Darkmatter.Theme.Sunrise => Brush.Parse("#F5E0C3"),
            Darkmatter.Theme.Frost => Brush.Parse("#D0E1F9"),
            _ => Brush.Parse("#FFFFFF")
        };
    }

    public IBrush GetBorderBrush()
    {
        return GetTheme() switch
        {
            Darkmatter.Theme.Light => Brush.Parse("#E0E0E0"),
            Darkmatter.Theme.Dark => Brush.Parse("#2C2C2C"),
            Darkmatter.Theme.DarkHighContrast => Brush.Parse("#FFFFFF"),
            Darkmatter.Theme.Sunrise => Brush.Parse("#D7CCC8"),
            Darkmatter.Theme.Frost => Brush.Parse("#90A4AE"),
            _ => Brushes.Black
        };
    }

    public IBrush GetCardBGS()
    {
        return GetTheme() switch
        {
            Darkmatter.Theme.Light => Brush.Parse("#F9F9F9"),
            Darkmatter.Theme.Dark => Brush.Parse("#1F1F1F"),
            Darkmatter.Theme.DarkHighContrast => Brush.Parse("#000000"),
            Darkmatter.Theme.Sunrise => Brush.Parse("#FFE0B2"),
            Darkmatter.Theme.Frost => Brush.Parse("#BBDEFB"),
            _ => Brush.Parse("#FFFFFF")
        };
    }

    public IBrush getTextTheme()
    {
        return GetTheme() switch
        {
            Darkmatter.Theme.Light => Brush.Parse("#212121"),
            Darkmatter.Theme.Dark => Brush.Parse("#E0E0E0"),
            Darkmatter.Theme.DarkHighContrast => Brush.Parse("#FFFFFF"),
            Darkmatter.Theme.Sunrise => Brush.Parse("#6D4C41"),
            Darkmatter.Theme.Frost => Brush.Parse("#0D47A1"),
            _ => Brush.Parse("#FFFFFF")
        };
    }

    public IBrush GetGroupText()
    {
        return GetTheme() switch
        {
            Darkmatter.Theme.DarkHighContrast => Brush.Parse("#000000"),
            _ => Brush.Parse("#FFFFFF")
        };
    }

    public IBrush GetSecondaryText()
    {
        return GetTheme() switch
        {
            Darkmatter.Theme.Light => Brush.Parse("#666666"),
            Darkmatter.Theme.Dark => Brush.Parse("#B0B0B0"),
            Darkmatter.Theme.DarkHighContrast => Brush.Parse("#FFFFFF"),
            Darkmatter.Theme.Sunrise => Brush.Parse("#6D4C41"),
            Darkmatter.Theme.Frost => Brush.Parse("#546E7A"),
            _ => Brush.Parse("#FFFFFF")
        };
    }

    public static async Task<(bool UpdateAvailable, Version LatestVersion)> IsUpdateAvailable(Version currentVersion)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ModManager");

            string url = "https://raw.githubusercontent.com/baggZ-idk/Darkmatter/refs/heads/main/version";
            string response = await client.GetStringAsync(url);
            var latestVersion = Version.Parse(response);

            return (currentVersion < latestVersion, latestVersion);
        }
        catch
        {
            return (false, currentVersion);
        }
    }
    #endregion
    
    #region Configs And Stuff
    private static string GetConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "Darkmatter");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "config.json");
    }

    public static Theme GetTheme()
    {
        try
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config { Theme = Darkmatter.Theme.Light };
                File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                return defaultConfig.Theme;
            }

            var json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<Config>(json);

            if (config?.Theme != null)
            {
                return config.Theme;
            }
            else
            {
                return Darkmatter.Theme.Light;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading theme: {ex}");
            return Darkmatter.Theme.Light;
        }
    }
    #endregion

    #region Mod Loading
    private readonly HashSet<string> BlacklistedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BepInEx"
        // made it like this because i might add more later but idk
    };

    private static readonly HttpClient Http = new();

    private static readonly string RepositoryConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Darkmatter",
        "repositories.json");

    public async Task LoadModsFromTheNewGitHubRepoAsync()
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(RepositoryConfigPath)!);

            if (!File.Exists(RepositoryConfigPath))
            {
                var defaults = new List<Repository>
                {
                    new()
                    {
                        Name = "Darkmatter Official",
                        Url = "https://raw.githubusercontent.com/baggZ-idk/Darkmatter/refs/heads/main/Data/modinfo.json",
                        Enabled = true
                    }
                };

                await File.WriteAllTextAsync(
                    RepositoryConfigPath,
                    JsonConvert.SerializeObject(defaults, Formatting.Indented));
            }

            var repositories = JsonConvert.DeserializeObject<List<Repository>>(
                await File.ReadAllTextAsync(RepositoryConfigPath))
                ?? new List<Repository>();

            var allMods = new List<Mod>();
            int failedRepositories = 0;

            foreach (var repository in repositories.Where(r => r.Enabled))
            {
                try
                {
                    if (!Uri.TryCreate(repository.Url, UriKind.Absolute, out var uri)
                        || uri.Scheme != Uri.UriSchemeHttps)
                    {
                        throw new InvalidOperationException(
                            "Repository URL must use HTTPS.");
                    }

                    var json = await Http.GetStringAsync(uri);
                    var mods = JsonConvert.DeserializeObject<List<Mod>>(json);

                    if (mods == null)
                        throw new InvalidDataException("Invalid mod list JSON.");

                    allMods.AddRange(mods);

                    Console.WriteLine(
                        $"Loaded {mods.Count} mods from {repository.Name}");
                }
                catch (Exception ex)
                {
                    failedRepositories++;
                    Console.WriteLine(
                        $"Failed to load {repository.Name}: {ex.Message}");
                }
            }

            Mods.Clear();
            ItemControl0.Items.Clear();

            var filtered = allMods
                .Where(m => !BlacklistedMods.Contains(
                    m.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var grouped = filtered
                .GroupBy(m => string.IsNullOrWhiteSpace(m.Group)
                    ? "Uncategorized"
                    : m.Group)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                AddHeader($"-- {group.Key} --");

                foreach (var mod in group.OrderBy(m => m.Name))
                {
                    Mods.Add(mod);
                    ItemControl0.Items.Add(MakeModControl(mod));
                }
            }

            Console.WriteLine(
                $"Loaded {filtered.Count} mods; " +
                $"skipped {allMods.Count - filtered.Count} blacklisted mods; " +
                $"{failedRepositories} repositories failed.");

            if (repositories.Count(r => r.Enabled) == 0)
            {
                await ShowErrorMessage("No repositories are enabled.");
            }
            else if (allMods.Count == 0 && failedRepositories > 0)
            {
               await ShowErrorMessage(
                    "Couldn't load mods from any enabled repository.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Loading mods failed: {ex.Message}");
            await ShowErrorMessage("Couldn't load the mod list.");
        }

        closeStartWindow();
        Opacity = 1;
    }

    
    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text ?? "");
    }
    private void ApplyFilter(string query)
    {
        query = query.Trim().ToLowerInvariant();

        ItemControl0.Items.Clear();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? Mods.ToList()
            : Mods.Where(m =>
                    m.Name?.ToLowerInvariant().Contains(query) == true ||
                    m.Author?.ToLowerInvariant().Contains(query) == true ||
                    m.Group?.ToLowerInvariant().Contains(query) == true)
                .ToList();

        switch (SortBox.SelectedIndex)
        {
            case 0:
                var grouped = filtered
                    .GroupBy(m => string.IsNullOrWhiteSpace(m.Group) ? "Uncategorized" : m.Group)
                    .OrderBy(g => g.Key);
                foreach (var group in grouped)
                {
                    AddHeader($"-- {group.Key} --");
                    foreach (var mod in group.OrderBy(m => m.Name))
                        ItemControl0.Items.Add(MakeModControl(mod));
                }
                break;
            case 1:
                var groupedByLetter = filtered
                    .GroupBy(m => string.IsNullOrWhiteSpace(m.Name) ? "#" : m.Name[0].ToString().ToUpperInvariant())
                    .OrderBy(g => g.Key);
                foreach (var group in groupedByLetter)
                {
                    AddHeader($"-- {group.Key} --");
                    foreach (var mod in group.OrderBy(m => m.Name))
                        ItemControl0.Items.Add(MakeModControl(mod));
                }
                break;

            case 2:
                var groupedByAuthor = filtered
                    .GroupBy(m => string.IsNullOrWhiteSpace(m.Author) ? "Unknown Author" : m.Author)
                    .OrderBy(g => g.Key);
                foreach (var group in groupedByAuthor)
                {
                    AddHeader($"-- {group.Key} --");
                    foreach (var mod in group.OrderBy(m => m.Name))
                        ItemControl0.Items.Add(MakeModControl(mod));
                }
                break;
        }
    }
    private void AddHeader(string text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = getTextTheme(),
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 6, 0, 2)
        };
        ItemControl0.Items.Add(new Label { Content = textBlock });
        OtherGroupTextBlocks.Add(textBlock);
    }
    
    private void SortBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Mods == null || ItemControl0 == null)
            return;

        ApplyFilter(SearchBox?.Text ?? "");
    }
    #endregion

    #region UI And Stuff
    private async Task SaveConfig(string path, Theme theme)
    {
        gamePath = path;
        CurrentTheme = theme;
        var config = new Config { GamePath = path, Theme = theme};
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        await File.WriteAllTextAsync(GetConfigPath(), json);
        MessageBox0.Text = "Game path saved successfully!";
    }
    
    private void ShowDropdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.Open(button);
        }
    }

    void SwitchTheme(object sender, RoutedEventArgs e)
    {
        var currentTheme = GetTheme();
        var themes = Enum.GetValues<Theme>();
        var currentIndex = Array.IndexOf(themes, currentTheme);
        var nextIndex = (currentIndex + 1) % themes.Length;
            
        CurrentTheme = themes[nextIndex];

        var config = new Config { GamePath = GetGamePath(), Theme = CurrentTheme };
        var json = JsonConvert.SerializeObject(config);
        File.WriteAllText(GetConfigPath(), json);
        
        InitUI();
        foreach (var border in modBorders)
        {
            border.border.Background = GetCardBGS();
            border.border.BorderBrush = GetGroupColor(border.ModAssigned.Group);
        }

        if (textBlocks.Any())
        {
            foreach (var textBlock in textBlocks)
            {
                textBlock.Foreground = getTextTheme();
            }
        }

        if (secondaryTextBlocks.Any())
        {
            foreach (var textBlock in secondaryTextBlocks)
            {
                textBlock.Foreground = GetSecondaryText();
            }
        }

        if (GroupTextBlocks.Any())
        {
            foreach (var textBlock in GroupTextBlocks)
            {
                textBlock.Foreground = GetGroupText();
            }
        }

        if (OtherGroupTextBlocks.Any())
        {
            foreach (var textBlock in OtherGroupTextBlocks)
            {
                textBlock.Foreground = getTextTheme();
            }
        }
    }
    
    string GetRepoUrl(string fullUrl)
    {
        if (string.IsNullOrEmpty(fullUrl))
            return fullUrl;
        try
        {
            var uri = new Uri(fullUrl);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2)
            {
                var owner = segments[0];
                var repo = segments[1];
                return $"{uri.Scheme}://{uri.Host}/{owner}/{repo}";
            }
        }
        catch
        {
        }

        return fullUrl;
    }

    private Border MakeModControl(Mod mod)
    {
        var border = new Border
        {
            Tag = mod.Name,
            BorderThickness = new Thickness(2),
            BorderBrush = GetGroupColor(mod.Group),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(6),
            Padding = new Thickness(12),
            Background = Brushes.White
        };

        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var contentStack = new StackPanel
        {
            Spacing = 4
        };

        var nameTextBlock = new TextBlock
        {
            Text = mod.Name,
            FontSize = 18,
            FontWeight = FontWeight.Bold
        };
        var infoPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        TextBlock authorTextBlock = null;
        if (!string.IsNullOrEmpty(mod.Author))
        {
            authorTextBlock = new TextBlock
            {
                Text = mod.AuthorInfo,
                FontSize = 12,
                FontStyle = FontStyle.Italic
            };
            infoPanel.Children.Add(authorTextBlock);
        }

        TextBlock groupText = null;
        if (!string.IsNullOrEmpty(mod.Group))
        {
            var groupBadge = new Border
            {
                Background = GetGroupColor(mod.Group),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2)
            };
            groupText = new TextBlock
            {
                Text = mod.Group,
                FontSize = 10,
                FontWeight = FontWeight.Bold
            };
            groupBadge.Child = groupText;
            infoPanel.Children.Add(groupBadge);
        }

        TextBlock dependenciesText = null;
        if (mod.Dependencies?.Any() == true)
        {
            dependenciesText = new TextBlock
            {
                Text = $"Dependencies: {string.Join(", ", mod.Dependencies)}",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            };
        }

        var urlTextBlock = new TextBlock
        {
            Text = ShortenUrl(mod.DownloadUrl),
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        };

        var isInstalled = IsModInstalled(mod);
        var statusText = new TextBlock
        {
            Name = "status",
            Text = isInstalled ? "\u2713 Installed" : "Not installed",
            FontSize = 12,
            Foreground = isInstalled ? Brushes.Green : Brushes.Orange
        };

        var openInGithub = new TextBlock()
        {
            Text = "Open In Github",
            Foreground = GetSecondaryText(),
            FontSize = 10,
        };
        openInGithub.PointerPressed += (s, e) =>
        {
            var githubLink = GetRepoUrl(mod.DownloadUrl);
            if (!string.IsNullOrEmpty(githubLink))
            {
                Process.Start(new ProcessStartInfo(githubLink) { UseShellExecute = true });
            }
        };

        contentStack.Children.Add(nameTextBlock);
        contentStack.Children.Add(infoPanel);
        if (dependenciesText != null)
            contentStack.Children.Add(dependenciesText);
        contentStack.Children.Add(urlTextBlock);
        contentStack.Children.Add(openInGithub);
        contentStack.Children.Add(statusText);

        var buttonStack = new StackPanel
        {
            Name = "ButtonStack",
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var installButton = new Button
        {
            Name = "InstallButton",
            Content = isInstalled ? "Reinstall" : "Install",
            Background = isInstalled ? Brushes.Orange : Brushes.Green,
            Foreground = Brushes.White,
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(4)
        };

        installButton.Click += async (s, e) =>
        {
            await InstallMod(mod, installButton, statusText);
        };

        var uninstallButton = new Button
        {
            Content = "Uninstall",
            Background = Brushes.Red,
            Foreground = Brushes.White,
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(4),
            IsVisible = isInstalled
        };

        uninstallButton.Click += async (s, e) =>
        {
            await UninstallMod(
                mod,
                installButton,
                uninstallButton,
                statusText);
        };

        buttonStack.Children.Add(uninstallButton);
        buttonStack.Children.Add(installButton);

        Grid.SetColumn(contentStack, 0);
        Grid.SetColumn(buttonStack, 1);

        mainGrid.Children.Add(contentStack);
        mainGrid.Children.Add(buttonStack);
        contentStack.Name = "ContentStack";

        border.Child = mainGrid;
        var thisBorder = new ModBorderThingyForDictionary
        {
            border = border,
            InstallButton = installButton,
            StatusText = statusText,
            UninstallButton = uninstallButton,
            ModAssigned = mod,
        };
        
        modBorders.Add(thisBorder);
        border.Background = GetCardBGS();
        nameTextBlock.Foreground = getTextTheme();
        textBlocks.Add(nameTextBlock);
        if (authorTextBlock != null)
        {
            authorTextBlock.Foreground = getTextTheme();
            textBlocks.Add(authorTextBlock);
        }
        urlTextBlock.Foreground = getTextTheme();
        textBlocks.Add(urlTextBlock);
        if (dependenciesText != null)
        {
            dependenciesText.Foreground = GetSecondaryText();
            secondaryTextBlocks.Add(dependenciesText);
        }
        secondaryTextBlocks.Add(openInGithub);

        if (groupText != null)
        {
            groupText.Foreground = GetGroupText();
            GroupTextBlocks.Add(groupText);
        }
        ModControls.Add(border);
        return border;
    }

    void MakeNotificationThing()
    {
        var bellStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10)
        };
        var bellButton = new Button
        {
            Background = Brushes.Transparent,
            BorderBrush = null,
            Content = new Image
            {
                Source = new Bitmap(AssetLoader.Open(new Uri("avares://Darkmatter/Assets/doorbell-google.png"))),
                Width = 28,
                Height = 28,
                Stretch = Stretch.Uniform
            }
        };

        bellButton.Click += BellButton_Click;
        badgeBorder = new Border
        {
            Background = Brushes.Red,
            CornerRadius = new CornerRadius(10),
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = (badgeText = new TextBlock
            {
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Text = "0"
            }),
            IsVisible = false
        };
        bellStack.Children.Add(bellButton);
        bellStack.Children.Add(badgeBorder);

        MainGrid.Children.Add(bellStack);
        notificationPopup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = bellButton,
            Child = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(5),
                Width = 250,
                Padding = new Thickness(10),
                Child = BuildNotificationList()
            }
        };

        MainGrid.Children.Add(notificationPopup);
        notificationManager.OnChanged += UpdateUI;
    }
    
    private void BellButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (notificationManager.Notifications.Count > 0)
        {
            notificationPopup.IsOpen = !notificationPopup.IsOpen;

            if (notificationPopup.IsOpen)
                notificationManager.MarkAllAsRead();
        }
    }

    private StackPanel BuildNotificationList()
    {
        var stack = new StackPanel();

        foreach (var notif in notificationManager.Notifications)
        {
            var notifPanel = new StackPanel { Margin = new Thickness(5) };

            var title = new TextBlock
            {
                Text = notif.Title,
                FontWeight = FontWeight.Bold
            };
            var message = new TextBlock
            {
                Text = notif.Message,
                FontSize = 12
            };
            notifPanel.Children.Add(message);
            notifPanel.Children.Add(title);
            stack.Children.Add(notifPanel);
        }
        return stack;
    }

    private void UpdateUI()
    {
        int unread = notificationManager.UnreadCount;

        badgeText.Text = unread.ToString();
        badgeBorder.IsVisible = unread > 0;
        if (notificationPopup.Child is Border border && border.Child is StackPanel stack)
        {
            stack.Children.Clear();
            foreach (var notif in notificationManager.Notifications)
            {
                var notifPanel = new StackPanel { Margin = new Thickness(5) };

                notifPanel.Children.Add(new TextBlock
                {
                    Text = notif.Title,
                    FontWeight = FontWeight.Bold
                });

                notifPanel.Children.Add(new TextBlock
                {
                    Text = notif.Message,
                    FontSize = 12
                });

                stack.Children.Add(notifPanel);
            }
        }
    }

    private IBrush GetGroupColor(string group)
    {
        switch (CurrentTheme)
        {
            case Darkmatter.Theme.DarkHighContrast:
                return group?.ToLower() switch
                {
                    _ => Brush.Parse("#FFFFFF")
                };
            default:
                return group?.ToLower() switch
                {
                    "libraries" => Brushes.Purple,
                    "mod menu" => Brushes.Orange,
                    "cosmetic" => Brushes.HotPink,
                    "competitive" => Brushes.Red,
                    "gameplay" => Brushes.Green,
                    "quality of life" => Brushes.DodgerBlue,
                    "mod checker" => Brushes.Yellow,
                    _ => Brushes.LightGray
                };
        }
    }
    private string ShortenUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || url.Length <= 50)
            return url;
        
        return url.Substring(0, 47) + "...";
    }

    private bool IsModInstalled(Mod mod)
    {
        if (string.IsNullOrEmpty(pluginsPath) || !Directory.Exists(pluginsPath))
            return false;
            
        var downloadUrl = mod.DownloadUrl;
        if (string.IsNullOrEmpty(downloadUrl))
            return false;
            
        try
        {
            var uri = new Uri(downloadUrl);
            var fileName = Path.GetFileName(uri.LocalPath);
            
            if (string.IsNullOrEmpty(fileName))
                return false;

            var installLocation = !string.IsNullOrEmpty(mod.InstallLocation) 
                ? mod.InstallLocation 
                : "BepInEx/plugins";
            var targetDirectory = Path.Combine(gamePath, installLocation);

            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var trackingFile = Path.Combine(appData, "Darkmatter", $".{mod.Name}_installed.txt");
                return File.Exists(trackingFile);
            }
            else
            {
                var targetPath = Path.Combine(targetDirectory, fileName);
                if (File.Exists(targetPath))
                    return true;
                var files = Directory.GetFiles(targetDirectory, fileName, SearchOption.AllDirectories);
                return files.Length > 0;
            }
        }
        catch (Exception ex)
        {
            return false;
        }
    }
#endregion

    #region Installation
    private void ExtractZipToFolder(string zipPath, string targetDirectory)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var entryPath = entry.FullName.Replace('\\', '/');
            if (entryPath.Contains("../") || Path.IsPathRooted(entryPath))
                continue;

            var destinationPath = Path.Combine(targetDirectory, entry.FullName);
            
            var fullDestinationPath = Path.GetFullPath(destinationPath);
            var fullTargetPath = Path.GetFullPath(targetDirectory);
            
            if (!fullDestinationPath.StartsWith(fullTargetPath, StringComparison.OrdinalIgnoreCase))
                continue;

            var directory = Path.GetDirectoryName(destinationPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }



    private async Task InstallMod(Mod mod, Button installButton, TextBlock statusText, Mod ModSender = null)
    {
        if (mod == null)
            throw new ArgumentNullException(nameof(mod));

        try
        {
            if (installButton != null)
            {
                installButton.IsEnabled = false;
                installButton.Content = "Installing...";
            }

            MessageBox0.Text = $"Installing {mod.Name}...";

            var installLocation = !string.IsNullOrEmpty(mod.InstallLocation)
                ? mod.InstallLocation
                : "BepInEx/plugins";

            var targetDirectory = Path.Combine(gamePath, installLocation);
            Directory.CreateDirectory(targetDirectory);

            var downloadUrl = mod.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
                throw new InvalidOperationException("Mod download URL is empty");

            var uri = new Uri(downloadUrl);
            var fileName = Path.GetFileName(uri.LocalPath);

            if (string.IsNullOrEmpty(fileName))
                fileName = $"{mod.Name}.dll";

            var downloadPath = await DownloadFile(downloadUrl, fileName);

            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var installedFiles = new List<string>();

                using (var archive = ZipFile.OpenRead(downloadPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        var entryPath = entry.FullName.Replace('\\', '/');

                        if (entryPath.Contains("../") || Path.IsPathRooted(entryPath))
                            continue;

                        var destinationPath = Path.Combine(targetDirectory, entry.FullName);
                        var fullDestinationPath = Path.GetFullPath(destinationPath);
                        var fullTargetPath = Path.GetFullPath(targetDirectory);

                        if (!fullDestinationPath.StartsWith(
                                fullTargetPath + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(
                                fullDestinationPath,
                                fullTargetPath,
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        var directory = Path.GetDirectoryName(destinationPath);

                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);

                        entry.ExtractToFile(destinationPath, overwrite: true);

                        installedFiles.Add(
                            Path.GetRelativePath(targetDirectory, destinationPath));
                    }
                }

                var appData = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);

                var trackingDirectory = Path.Combine(appData, "Darkmatter");
                Directory.CreateDirectory(trackingDirectory);

                var trackingFile = Path.Combine(
                    trackingDirectory, $".{mod.Name}_installed.txt");

                await File.WriteAllLinesAsync(trackingFile, installedFiles);

                if (File.Exists(downloadPath))
                    File.Delete(downloadPath);
            }
            else
            {
                var modFolder = Path.Combine(targetDirectory, mod.Name);
                Directory.CreateDirectory(modFolder);

                var targetPath = Path.Combine(modFolder, fileName);

                if (File.Exists(targetPath))
                    File.Delete(targetPath);

                File.Move(downloadPath, targetPath);
            }

            statusText.Text = "✓ Installed";
            statusText.Foreground = Brushes.Green;

            installButton.Content = "Reinstall";
            installButton.Background = Brushes.Orange;

            var modEntry = modBorders.FirstOrDefault(
                x => x.ModAssigned.Name == mod.Name);

            if (modEntry?.UninstallButton != null)
                modEntry.UninstallButton.IsVisible = true;

            MessageBox0.Text = $"Successfully installed {mod.Name}!";
        }
        catch (Exception ex)
        {
            await ShowErrorMessage($"Failed to install {mod.Name}: {ex.Message}");

            statusText.Text = "Installation failed";
            statusText.Foreground = Brushes.Red;
    
            installButton.Content = "Install";
            installButton.Background = Brushes.Green;
        }
        finally
        {
            if (installButton != null)
                installButton.IsEnabled = true;
        }

        if (mod.Dependencies?.Any() == true)
        {
            foreach (var dependency in mod.Dependencies)
            {
                foreach (var modBorder in modBorders.ToList())
                {
                    if (modBorder.ModAssigned != ModSender &&
                        modBorder.ModAssigned.Name == dependency)
                    {
                        await InstallMod(
                            modBorder.ModAssigned,
                            modBorder.InstallButton,
                            modBorder.StatusText,
                            mod);
                    }
                }
            }
        }
    }



    private async Task UninstallMod(Mod mod, Button installButton, Button uninstallButton, TextBlock statusText)
    {
        if (mod == null)
            throw new ArgumentNullException(nameof(mod));

        try
        {
            var installLocation = !string.IsNullOrEmpty(mod.InstallLocation) 
                ? mod.InstallLocation 
                : "BepInEx/plugins";
            
            var targetDirectory = Path.Combine(gamePath, installLocation);
            var fileDeleted = false;
            
            var downloadUrl = mod.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                MessageBox0.Text = $"Cannot uninstall {mod.Name}: No download URL available.";
                return;
            }

            var uri = new Uri(downloadUrl);
            var fileName = Path.GetFileName(uri.LocalPath);
            
            if (string.IsNullOrEmpty(fileName))
            {
                MessageBox0.Text = $"Cannot uninstall {mod.Name}: Cannot determine filename from URL.";
                return;
            }

            if (Directory.Exists(targetDirectory))
            {
                if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var trackingFile = Path.Combine(appData, "Darkmatter", $".{mod.Name}_installed.txt");
                    
                    if (File.Exists(trackingFile))
                    {
                        var installedFiles = await File.ReadAllLinesAsync(trackingFile);
                        
                        foreach (var relativePath in installedFiles)
                        {
                            var fullPath = Path.Combine(targetDirectory, relativePath);
                            if (File.Exists(fullPath))
                            {
                                try
                                {
                                    File.Delete(fullPath);
                                    fileDeleted = true;
                                }
                                catch
                                {
                                    // idk
                                }
                            }
                        }
                        
                        try
                        {
                            File.Delete(trackingFile);
                        }
                        catch
                        {
                            // another part where idk
                        }
                        
                        var directoriesToCheck = installedFiles
                            .Select(f => Path.GetDirectoryName(Path.Combine(targetDirectory, f)))
                            .Where(d => !string.IsNullOrEmpty(d))
                            .Distinct()
                            .OrderByDescending(d => d.Length);
                        
                        foreach (var dir in directoriesToCheck)
                        {
                            try
                            {
                                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                                {
                                    Directory.Delete(dir);
                                }
                            }
                            catch
                            {
                                // writing to make rider shut up
                            }
                        }
                    }
                    else
                    {
                        MessageBox0.Text = $"Cannot uninstall {mod.Name}: No installation tracking found. This mod may have been installed with an older version of the manager.";
                        return;
                    }
                }
                else
                {
                    var targetPath = Path.Combine(targetDirectory, fileName);
                    
                    if (File.Exists(targetPath))
                    {
                        try
                        {
                            File.Delete(targetPath);
                            fileDeleted = true;
                        }
                        catch
                        {
                            // rider
                        }
                    }
                    else
                    {
                        var files = Directory.GetFiles(targetDirectory, fileName, SearchOption.AllDirectories);
                        
                        foreach (var file in files)
                        {
                            try
                            {
                                File.Delete(file);
                                fileDeleted = true;
                                break;
                            }
                            catch
                            {
                                // gibb banan
                            }
                        }
                    }
                }
            }

            if (fileDeleted)
            {
                statusText.Text = "Not installed";
                statusText.Foreground = Brushes.Orange;
                installButton.Content = "Install";
                installButton.Background = Brushes.Green;

                if (uninstallButton.Parent is StackPanel parent)
                {
                    uninstallButton.IsVisible = false;
                }

                MessageBox0.Text = $"Successfully uninstalled {mod.Name}!";
            }
            else
            {
                MessageBox0.Text = $"Could not find {mod.Name} files to uninstall.";
            }
        }
        catch (Exception ex)
        {
            await ShowErrorMessage($"Could not uninstall {mod.Name}: {ex.Message}");
        }
    }

    private async Task<string> DownloadFile(string url, string fileName)
    {
        try
        {
            var fileBytes = await httpClient.GetByteArrayAsync(url);
            var downloadDir = Path.GetTempPath();
            Directory.CreateDirectory(downloadDir);
            
            var filePath = Path.Combine(downloadDir, fileName);
            await File.WriteAllBytesAsync(filePath, fileBytes);
            
            return filePath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Download failed: {ex.Message}");
        }
    }

    

    private async Task<bool> CheckOrInstallBepInEx()
    {
        try
        {
            var bepInExPath = Path.Combine(gamePath, "BepInEx");
            
            if (!Directory.Exists(bepInExPath))
            {
                MessageBox0.Text = "BepInEx not found. Downloading...";
                
                var bytes = await httpClient.GetByteArrayAsync(
                    "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x64_5.4.23.3.zip");
                
                var tempPath = Path.Combine(Path.GetTempPath(), $"BepInEx_{Guid.NewGuid()}.zip");
                await File.WriteAllBytesAsync(tempPath, bytes);
                
                MessageBox0.Text = "Extracting BepInEx please wait!!!!!";
                await ExtractBepInEx(tempPath);
                
                File.Delete(tempPath);
                MessageBox0.Text = "BepInEx installed successfully!";
                
                return true;
            }
            
            MessageBox0.Text = "BepInEx already installed.";
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorMessage($"couldnt install bepinex: {ex.Message}");
            return false;
        }
    }

    private async Task ExtractBepInEx(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var destinationPath = Path.Combine(gamePath, entry.FullName);
                var directory = Path.GetDirectoryName(destinationPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"couldnr extract {ex.Message}");
        }
    }
    private async Task InstallFromDisk(string modPath = null)
    {
        try
        {
            if (modPath == null)
            {
                MessageBox0.Text = "Installing...";

                var filePickerOptions = new FilePickerOpenOptions()
                {
                    Title = "Select Mod File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("DLL Files") { Patterns = new[] { "*.dll" } }
                    }
                };

                var files = await MainWin.StorageProvider.OpenFilePickerAsync(filePickerOptions);

                if (files == null || files.Count == 0) return;

                var selectedFile = files[0];
                var fileName = selectedFile.Name;
                var sourcePath = selectedFile.Path.LocalPath;
                var modFolderName = Path.GetFileNameWithoutExtension(fileName);
                var targetDirectory = Path.Combine(gamePath, "BepInEx", "plugins", modFolderName);

                Directory.CreateDirectory(targetDirectory);

                var targetPath = Path.Combine(targetDirectory, fileName);
                File.Copy(sourcePath, targetPath, overwrite: true);

                MessageBox0.Text = $"Successfully installed {fileName}!";
            }
            else
            {
                var fileName = Path.GetFileName(modPath);
                var modFolderName = Path.GetFileNameWithoutExtension(fileName);
                var targetDirectory = Path.Combine(gamePath, "BepInEx", "plugins", modFolderName);

                Directory.CreateDirectory(targetDirectory);

                var targetPath = Path.Combine(targetDirectory, fileName);
                File.Copy(modPath, targetPath, overwrite: true);

                MessageBox0.Text = $"Successfully installed {fileName}!";
            }
        }
        catch (Exception ex)
        {
            await ShowErrorMessage($"Failed to install from disk: {ex.Message}");
            MessageBox0.Text = "Installation failed";
        }
    }


    private async Task SilentInstall(string url)
    {
        var file = await DownloadFile(url, "SilentInstall.dll");
        await InstallFromDisk(file);
    }
    private bool TryFindModByName(string modName, out Mod foundMod)
    {
        foundMod = Mods.FirstOrDefault(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));
        return foundMod != null;
    }

    private bool TryFindBorderByMod(Mod mod, out Border foundBorder)
    {
        foundBorder = ModControls.FirstOrDefault(m => m.Tag.ToString().Equals(mod.Name, StringComparison.OrdinalIgnoreCase));
        return foundBorder != null;
    }
    #endregion
    
    #region Game Path Stuff
    private string GetGamePath()
    {
        try
        {
            if (!File.Exists(GetConfigPath()))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GetConfigPath()));
                File.WriteAllText(GetConfigPath(), JsonConvert.SerializeObject(new Config
                {
                    GamePath = ""
                }, Formatting.Indented));
                return null;
            }

            var json = File.ReadAllText(GetConfigPath());
            var config = JsonConvert.DeserializeObject<Config>(json);
            
            if (!string.IsNullOrWhiteSpace(config?.GamePath) && Directory.Exists(config.GamePath))
            {
                return config.GamePath;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading the config{ex.Message}");
        }
        
        return null;
    }

    private async Task<string> ShowGamePathDialog()
    {
        var dialog = new Window
        {
            Title = "Select Game Path",
            Width = 500,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var stackPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15
        };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Select your game path:",
            FontSize = 14,
            FontWeight = FontWeight.Medium
        });

        var pathTextBox = new TextBox
        {
            IsReadOnly = true,
            Height = 32,
            Background = Brushes.LightGray
        };

        var browseButton = new Button
        {
            Content = "Browse",
            Width = 100,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Height = 32,
            IsEnabled = false
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Height = 32
        };

        string selectedPath = null;

        browseButton.Click += async (sender, e) =>
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Game Folder"
            };

            var result = await folderDialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                pathTextBox.Text = result;
                selectedPath = result;
                okButton.IsEnabled = true;
            }
        };

        okButton.Click += (sender, e) =>
        {
            dialog.Close();
        };

        cancelButton.Click += (sender, e) =>
        {
            selectedPath = null;
            dialog.Close();
        };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(okButton);

        stackPanel.Children.Add(pathTextBox);
        stackPanel.Children.Add(browseButton);
        stackPanel.Children.Add(buttonPanel);

        dialog.Content = stackPanel;

        await dialog.ShowDialog(this);
        return selectedPath;
    }
    #endregion
    
    #region Config Editor

    public static async Task ShowConfigPickerAsync(Window parent, string GamePath)
    {
        string configDir = Path.Combine(GamePath, "BepInEx", "config");

        var pickerWindow = new Window
        {
            Title = "Config Editor That Edits Config Files",
            Width = 400,
            Height = 600,
            Background = Instance.getBGForTheme(),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10),
            Spacing = 8
        };

        if (!Directory.Exists(configDir))
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Directory not found:\n{configDir}",
                Foreground = Brushes.Red
            });
        }
        else
        {
            var cfgFiles = Directory.GetFiles(configDir, "*.cfg", SearchOption.TopDirectoryOnly);
            if (cfgFiles.Length == 0)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = "No .cfg files found.",
                    FontStyle = FontStyle.Italic,
                    Foreground = Instance.getTextTheme()
                });
            }
            else
            {
                foreach (var file in cfgFiles.OrderBy(Path.GetFileName))
                {
                    var text = "a";
                    if (file.EndsWith("BepInEx.cfg"))
                    {
                        text = $"{Path.GetFileName(file)} - FOR EXPERIENCED USERS ONLY!";
                    }
                    else
                    {
                        text = Path.GetFileName(file);
                    }
                    var btn = new Button
                    {
                        Content = text,
                        Foreground = Brushes.White,
                        Background = Brush.Parse("#3F51B5"),
                        Tag = file,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    btn.Click += async (_, __) =>
                    {
                        try
                        {
                            var config = ConfigFile.Load(file);
                            var editor = new ConfigEditorDialog(config);
                            bool result = await editor.ShowDialog<bool>(pickerWindow);

                            if (result)
                            {
                                config.Save(file);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    };

                    stackPanel.Children.Add(btn);
                }
            }
        }

        pickerWindow.Content = new ScrollViewer
        {
            Content = stackPanel
        };

        await pickerWindow.ShowDialog(parent);
    }
    #endregion 

    #region Error Handling
    private async Task ShowErrorMessage(string message)
    {
        MessageBox0.Text = $"Error: {message}";
        Console.WriteLine($"Error: {message}");
        
        var errorDialog = new Window
        {
            Title = "an ERROR has errored your app",
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15
        };

        content.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        okButton.Click += (s, e) => errorDialog.Close();
        content.Children.Add(okButton);

        errorDialog.Content = content;
        await errorDialog.ShowDialog(this);
    }
    #endregion
    
    #region Version Checking Stuff

    private async Task NewVersionDialog(Version version)
    {
        var dialog = new Window
        {
            Title = "New Version Available!!!",
            Width = 500,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15
        };

        dialog.Content = content;

        var text = new TextBlock
        {
            Text = "New Version Available",
            TextWrapping = TextWrapping.Wrap,
        };

        var installBtn = new Button
        {
            Content = "Install",
        };
        installBtn.Click += async (sender, e) =>
        {
            var url = "https://github.com/baggZ-idk/Darkmatter/releases/latest";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };

        var closeBtn = new Button
        {
            Content = "Close",
        };
        closeBtn.Click += async (sender, e) =>
        {
            dialog.Close();
        };
        content.Children.Add(text);
        content.Children.Add(installBtn);
        content.Children.Add(closeBtn);
        var owner = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        await dialog.ShowDialog(owner);
    }
    #endregion
    
    #region cant think of name
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }

    private void LaunchBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://rungameid/1533390",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _ = ShowErrorMessage("Failed to launch game: " + ex.Message + "\nplease note that the launch game feature is steam only");
        }
    }
    
    private async void ManageRepositories_OnClick(
        object? sender, RoutedEventArgs e)
    {
        var window = new RepositoriesWindow();
        var saved = await window.ShowDialog<bool?>(this);

        if (saved == true)
            await LoadModsFromTheNewGitHubRepoAsync();
    }

    private void InstallFromDiskBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = InstallFromDisk();
    }
    
    private void Config_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = ShowConfigPickerAsync(this, GetGamePath());
    }

    void OpenModsFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        var ModsPath = Path.Combine(gamePath, "BepInEx", "plugins");
        OpenFolder(ModsPath);
    }

    void OpenGamePath_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolder(gamePath);
    }

    void DisableAll_OnClick(object? sender, RoutedEventArgs e)
    {
        string dllPath = Path.Combine(gamePath, "winhttp.dll");
        string disabledPath = Path.Combine(gamePath, "disabled.mods");
        try
        {
            if (!ModsDisabled())
            {
                File.Move(dllPath, disabledPath);
                Console.WriteLine("Mods disabled.");
                DisableEnable.Header = "Enable Mods";
                SendNeededMesages();
            }
            else if (File.Exists(disabledPath))
            {
                File.Move(disabledPath, dllPath);
                Console.WriteLine("Mods re-enabled.");
                DisableEnable.Header = "Disable Mods";
                SendNeededMesages();
            }
            else
            {
                Console.WriteLine("No mods found to disable/enable.");
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"didnt work {ex}");
        }
    }

    void InstallAll(object? sender, RoutedEventArgs e)
    {
        foreach (var mod in modBorders)
        {
            _ = InstallMod(mod.ModAssigned, mod.InstallButton, mod.StatusText);
        }
    }

    void OpenFolder(string Path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo() { FileName = Path, UseShellExecute = true });
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", $"\"{Path}\"");
        }
    }
    private void ResetGamePath_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = ChangeTheGamePathBecauseINeedTaskForSomeReason();
    }

    async Task ChangeTheGamePathBecauseINeedTaskForSomeReason()
    {
        var path = await ShowGamePathDialog();
        await SaveConfig(path, GetTheme());
        await CheckOrInstallBepInEx();
    }
    #endregion
}

#region other classes/enums
public enum Theme
{
    Light,
    Dark,
    DarkHighContrast,
    Sunrise,
    Frost
}

public class Mod
{
    public string Name { get; set; }
    public string Author { get; set; }
    public List<string> Dependencies { get; set; } = new List<string>();
    public List<Mod> DependenciesAsMod { get; set; } = new List<Mod>();
    [JsonProperty("install_location")]
    public string InstallLocation { get; set; }
    [JsonProperty("git_path")]
    public string GitPath { get; set; }
    public string Group { get; set; }
    [JsonProperty("download_url")]
    public string DownloadUrl { get; set; }
    public string URL => DownloadUrl;
    public string AuthorInfo => !string.IsNullOrEmpty(Author) ? $"by {Author}" : "";
}

public class Repository
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class Config
{
    public string GamePath { get; set; }
    public Theme Theme { get; set; }
}

public class ModBorderThingyForDictionary
{
    public Border border { get; set; }
    public Button InstallButton { get; set; }
    public TextBlock StatusText { get; set; }
    public Button UninstallButton { get; set; }
    public Mod ModAssigned { get; set; }
}
#endregion
// you made it till the end!
