
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Newtonsoft.Json;

namespace Darkmatter;

public partial class RepositoriesWindow : Window
{
    private static readonly HttpClient Http = new();

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Darkmatter",
        "repositories.json");

    private readonly List<Repository> _repositories = new();

    public RepositoriesWindow()
    {
        InitializeComponent();

        var main = MainWindow.Instance;
        Background = main.getBGForTheme();
        Foreground = main.getTextTheme();
        RootGrid.Background = main.getBGForTheme();

        LoadRepositories();
        RenderRepositories();
    }

    private void LoadRepositories()
    {
        if (!File.Exists(ConfigPath))
            return;

        try
        {
            _repositories.AddRange(
                JsonConvert.DeserializeObject<List<Repository>>(
                    File.ReadAllText(ConfigPath))
                ?? new List<Repository>());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Repository config failed: {ex.Message}");
        }
    }

    private void RenderRepositories()
    {
        RepositoryList.Children.Clear();

        var main = MainWindow.Instance;

        foreach (var repository in _repositories)
        {
            var nameCheckBox = new CheckBox
            {
                Content = repository.Name,
                IsChecked = repository.Enabled,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = main.getTextTheme()
            };

            nameCheckBox.IsCheckedChanged += (_, _) =>
                repository.Enabled = nameCheckBox.IsChecked == true;

            var urlText = new TextBlock
            {
                Text = repository.Url,
                Foreground = main.GetSecondaryText(),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var details = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    nameCheckBox,
                    urlText
                }
            };

            var removeButton = new Button
            {
                Content = "Remove",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            removeButton.Click += (_, _) =>
            {
                _repositories.Remove(repository);
                RenderRepositories();
            };

            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
                Background = main.GetCardBGS(),
                Margin = new Thickness(0, 0, 0, 4)
            };

            row.Children.Add(details);

            Grid.SetColumn(removeButton, 1);
            row.Children.Add(removeButton);

            RepositoryList.Children.Add(row);
        }
    }

    private async void AddRepository_Click(
        object? sender, RoutedEventArgs e)
    {
        var url = RepositoryUrlBox.Text?.Trim();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            await ShowMessage("Enter a valid HTTPS repository URL.");
            return;
        }

        if (_repositories.Any(r =>
                string.Equals(r.Url, url,
                    StringComparison.OrdinalIgnoreCase)))
        {
            await ShowMessage("That repository has already been added.");
            return;
        }

        try
        {
            var json = await Http.GetStringAsync(uri);
            var mods = JsonConvert.DeserializeObject<List<Mod>>(json);

            if (mods == null)
                throw new InvalidDataException("Invalid mod list JSON.");

            var name = RepositoryNameBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                await ShowMessage("Enter a repository name.");
                return;
            }

            var repository = new Repository
            {
                Name = name,
                Url = url!,
                Enabled = true
            };

            _repositories.Add(repository);
            RepositoryUrlBox.Text = "";
            RenderRepositories();

            await ShowMessage(
                $"Repository added. Found {mods.Count} mods.");
        }
        catch (Exception ex)
        {
            await ShowMessage($"Couldn't add repository: {ex.Message}");
        }
    }

    private async Task ShowMessage(string message)
    {
        var main = MainWindow.Instance;

        var button = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var dialog = new Window
        {
            Title = "Repositories",
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = main.getBGForTheme(),
            Foreground = main.getTextTheme(),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        Foreground = main.getTextTheme(),
                        TextWrapping = TextWrapping.Wrap
                    },
                    button
                }
            }
        };

        button.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(ConfigPath)!);

            File.WriteAllText(
                ConfigPath,
                JsonConvert.SerializeObject(
                    _repositories, Formatting.Indented));

            Close(true);
        }
        catch (Exception ex)
        {
            _ = ShowMessage($"Couldn't save repositories: {ex.Message}");
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
