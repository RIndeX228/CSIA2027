using CS_IA_2027.Contracts;
using CS_IA_2027.Scrapers;
using CS_IA_2027.Services;
using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace CS_IA_2027;

public partial class MainWindow : Window
{
	#region Properrties
	// Next to the .exe, not the project folder - see the csproj note in
	// the README about copying config.json to the output directory.
	private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

	private readonly WidgetScheduler _scheduler = new();
	private readonly List<WidgetSlot> _slots;

	// whatever was loaded last - the edit state works on this
	private Config _config = new();

	// The styles in Theme.xaml watch this to swap between the normal and the edit
	// layout, so setting it is all it takes to switch.
	public static readonly DependencyProperty IsEditingProperty =
		DependencyProperty.Register(nameof(IsEditing), typeof(bool), typeof(MainWindow));

	public bool IsEditing
	{
		get => (bool)GetValue(IsEditingProperty);
		set => SetValue(IsEditingProperty, value);
	}
	#endregion
	#region Contructor
	public MainWindow()
	{
		InitializeComponent();
		_slots = BuildSlots();
		_ = ReloadAsync();
	}
	#endregion
	#region Methods
	// One entry per card. The key has to match the widget name in config.json.
	private List<WidgetSlot> BuildSlots() => new()
	{
		new WidgetSlot("weather", WeatherCard, WeatherDataText, WeatherSecondaryText,
			WeatherEnabledSwitch, WeatherColourBox, WeatherRateBox, WeatherSettingsBox,
			"WeatherAccentBrush", "WeatherSecondaryBrush", "WeatherCardBackground",
			c => new WeatherScrapper(c.Latitude ?? WidgetConfig.DefaultLatitude, c.Longitude ?? WidgetConfig.DefaultLongitude, c.City)),

		new WidgetSlot("github", GitHubCard, GitHubDataText, GitHubSecondaryText,
			GitHubEnabledSwitch, GitHubColourBox, GitHubRateBox, null,
			"GitHubAccentBrush", "GitHubSecondaryBrush", "GitHubCardBackground",
			_ => new GitHubNotificationsScrapper()),

		new WidgetSlot("stocks", StockCard, StockDataText, StockSecondaryText,
			StockEnabledSwitch, StockColourBox, StockRateBox, StockSettingsBox,
			"StockAccentBrush", "StockSecondaryBrush", "StockCardBackground",
			c => new StockScrapper(c.Symbols ?? new[] { "AAPL", "MSFT" })),

		new WidgetSlot("news", NewsCard, NewsDataText, NewsSecondaryText,
			NewsEnabledSwitch, NewsColourBox, NewsRateBox, NewsSettingsBox,
			"NewsAccentBrush", "NewsSecondaryBrush", "NewsCardBackground",
			c => new RssScrapper(c.FeedUrl))
	};

	private async Task ReloadAsync()
	{
		var config = await Config.LoadOrCreateAsync(ConfigPath);
		Apply(config);
	}

	private void Apply(Config config)
	{
		_config = config;

		// Kill the old timers before building new ones, otherwise every
		// refresh doubles the number of requests going out.
		_scheduler.StopAll();

		var map = config.WidgetMap();

		foreach (var slot in _slots)
		{
			if (!map.TryGetValue(slot.Key, out var widget))
				widget = new WidgetConfig();

			ApplyColours(slot, widget);

			slot.Card.Visibility = widget.Enabled ? Visibility.Visible : Visibility.Collapsed;
			if (!widget.Enabled)
				continue;

			slot.Data.Text = "…";
			slot.Secondary.Text = "Loading";

			var source = slot.Build(widget);
			var interval = TimeSpan.FromSeconds(Math.Clamp(widget.UpdateRate, 5, 86400));   // 5 stops updateRate: 0 hammering the APIs, 86400 keeps DispatcherTimer from throwing on huge values
			_scheduler.Add(interval, ct => UpdateCardAsync(source, slot, ct));
		}

		Reorder(map);

		// applying always ends the edit state, so Refresh widgets in the tray also works as cancel
		IsEditing = false;
	}

	private static async Task UpdateCardAsync(IWidgetSource source, WidgetSlot slot, CancellationToken ct)
	{
		var result = await source.RefreshAsync(ct);
		slot.Data.Text = result.Success ? result.DisplayText : "—";
		slot.Secondary.Text = result.Success ? result.SecondaryText ?? "" : result.ErrorMessage ?? "Error";
	}

	// dim: 0 is the normal look, 0.35 means 35% of the way to black (used in the edit state)
	private static void ApplyColours(WidgetSlot slot, WidgetConfig widget, double dim = 0)
	{
		var accent = Dimmed(ParseColour(widget.PrimaryColor, Colors.White), dim);
		var secondary = Dimmed(ParseColour(widget.SecondaryColor, Colors.Gray), dim);

		var resources = Application.Current.Resources;
		resources[slot.AccentKey] = Frozen(new SolidColorBrush(accent));
		resources[slot.SecondaryKey] = Frozen(new SolidColorBrush(secondary));
		resources[slot.BackgroundKey] = Frozen(CardGradient(accent));
	}

	// Void Black sliding toward 22% of the accent - same rule the theme
	// file uses, just done here so any colour from the config still fits.
	private static LinearGradientBrush CardGradient(Color accent)
	{
		var black = Color.FromRgb(0x06, 0x07, 0x0A);
		var tint = Blend(black, accent, 0.22);

		return new LinearGradientBrush(
			new GradientStopCollection
			{
				new GradientStop(black, 0),
				new GradientStop(tint, 1)
			},
			new Point(0, 0), new Point(1, 1));
	}

	private static Color Dimmed(Color colour, double amount) =>
		Blend(colour, Color.FromRgb(0x06, 0x07, 0x0A), amount);

	private static Color Blend(Color a, Color b, double t) => Color.FromRgb(
		(byte)Math.Round(a.R * (1 - t) + b.R * t),
		(byte)Math.Round(a.G * (1 - t) + b.G * t),
		(byte)Math.Round(a.B * (1 - t) + b.B * t));

	private static Color ParseColour(string hex, Color fallback)
	{
		try
		{
			return ColorConverter.ConvertFromString(hex) is Color c ? c : fallback;
		}
		catch
		{
			return fallback;   // typo in the config shouldn't blank the card
		}
	}

	private static T Frozen<T>(T brush) where T : Freezable
	{
		brush.Freeze();
		return brush;
	}

	private void Reorder(Dictionary<string, WidgetConfig> map)
	{
		var ordered = _slots
			.OrderBy(s => map.TryGetValue(s.Key, out var w) ? w.Position : int.MaxValue)
			.ToList();

		CardStack.Children.Clear();
		foreach (var slot in ordered)
			CardStack.Children.Add(slot.Card);

		SpaceCards();
	}

	// gap between cards, but nothing hanging off the bottom one
	private void SpaceCards()
	{
		var visible = CardStack.Children.Cast<Border>()
			.Where(card => card.Visibility == Visibility.Visible)
			.ToList();

		for (int i = 0; i < visible.Count; i++)
			visible[i].Margin = new Thickness(0, 0, 0, i == visible.Count - 1 ? 0 : 8);
	}

	// ---------------- edit state ----------------

	// pencil
	private void Edit_Click(object sender, RoutedEventArgs e)
	{
		var map = _config.WidgetMap();

		foreach (var slot in _slots)
		{
			if (!map.TryGetValue(slot.Key, out var widget))
				widget = new WidgetConfig();

			ApplyColours(slot, widget, 0.35);   // dimmed, so it's obvious you're editing

			// disabled cards show up too, otherwise there'd be no way to switch them back on
			slot.Card.Visibility = Visibility.Visible;

			slot.EnabledSwitch.IsChecked = widget.Enabled;
			slot.ColourBox.Text = widget.PrimaryColor;
			slot.RateBox.Text = widget.UpdateRate.ToString(CultureInfo.InvariantCulture);
			if (slot.SettingsBox != null)
				slot.SettingsBox.Text = GetSettingsText(slot.Key, widget);
		}

		SpaceCards();
		IsEditing = true;
	}

	private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveCard(sender, -1);

	private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveCard(sender, 1);

	// the arrow buttons carry their widget's key in Tag
	private void MoveCard(object sender, int step)
	{
		var key = (string)((Button)sender).Tag;
		var slot = _slots.First(s => s.Key == key);

		int oldIndex = CardStack.Children.IndexOf(slot.Card);
		int newIndex = oldIndex + step;
		if (newIndex < 0 || newIndex >= CardStack.Children.Count)
			return;

		CardStack.Children.RemoveAt(oldIndex);
		CardStack.Children.Insert(newIndex, slot.Card);
		SpaceCards();
	}

	private async void Save_Click(object sender, RoutedEventArgs e)
	{
		if (ReadEditsIntoConfig())
			await SaveAndReloadAsync();
	}

	private async Task SaveAndReloadAsync()
	{
		try
		{
			await _config.SaveAsync(ConfigPath);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "Couldn't save the config");
			return;
		}

		await ReloadAsync();   // reads the file back, so what's on screen is what's saved
	}

	// Checks every box first, then copies the form into _config.
	// Returns false (and marks the bad boxes red) if something was typed wrong.
	private bool ReadEditsIntoConfig()
	{
		bool allValid = true;

		foreach (var slot in _slots)
		{
			slot.ColourBox.ClearValue(Control.BorderBrushProperty);
			if (!ColourIsValid(slot.ColourBox.Text))
			{
				slot.ColourBox.BorderBrush = Brushes.IndianRed;
				allValid = false;
			}

			slot.RateBox.ClearValue(Control.BorderBrushProperty);
			if (!RateIsValid(slot.RateBox.Text))
			{
				slot.RateBox.BorderBrush = Brushes.IndianRed;
				allValid = false;
			}

			if (slot.SettingsBox == null)
				continue;

			slot.SettingsBox.ClearValue(Control.BorderBrushProperty);
			if (!SettingsAreValid(slot.Key, slot.SettingsBox.Text))
			{
				slot.SettingsBox.BorderBrush = Brushes.IndianRed;
				allValid = false;
			}
		}

		if (!allValid)
			return false;

		var map = _config.WidgetMap();
		_config.Widgets.Clear();

		// the order of the cards on screen is the new order
		foreach (var card in CardStack.Children.Cast<Border>().ToList())
		{
			var slot = _slots.First(s => s.Card == card);

			if (!map.TryGetValue(slot.Key, out var widget))
				widget = new WidgetConfig();

			widget.Enabled = slot.EnabledSwitch.IsChecked == true;
			widget.PrimaryColor = slot.ColourBox.Text.Trim();
			widget.UpdateRate = int.Parse(slot.RateBox.Text.Trim(), CultureInfo.InvariantCulture);
			widget.Position = CardStack.Children.IndexOf(card);

			if (slot.SettingsBox != null)
				ApplySettingsText(slot.Key, widget, slot.SettingsBox.Text);

			_config.Widgets.Add(new Dictionary<string, WidgetConfig> { [slot.Key] = widget });
		}

		return true;
	}

	private static bool ColourIsValid(string text) =>
		Regex.IsMatch(text.Trim(), "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

	// whole seconds, 5 to a day. NumberStyles.None rejects signs, decimals and spaces.
	private static bool RateIsValid(string text) =>
		int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int seconds)
		&& seconds >= 5 && seconds <= 86400;

	// Only the feed url can really be wrong - a city or a list of symbols is whatever you type.
	private static bool SettingsAreValid(string key, string text)
	{
		text = text.Trim();
		if (key != "news" || text == "")
			return true;

		return Uri.TryCreate(text, UriKind.Absolute, out var uri)
			&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
	}

	// what goes into the settings box when editing starts
	private static string GetSettingsText(string key, WidgetConfig widget)
	{
		switch (key)
		{
			case "weather":
				if (!string.IsNullOrWhiteSpace(widget.City))
					return widget.City;

				// Invariant so it's 52.2297 and not 52,2297 on a Polish PC
				return FormattableString.Invariant(
					$"{widget.Latitude ?? WidgetConfig.DefaultLatitude}, {widget.Longitude ?? WidgetConfig.DefaultLongitude}");

			case "stocks":
				return string.Join(", ", widget.Symbols ?? new[] { "AAPL", "MSFT" });

			case "news":
				return widget.FeedUrl ?? "";

			default:
				return "";
		}
	}

	// the other way round: puts what was typed back into the widget's settings
	private static void ApplySettingsText(string key, WidgetConfig widget, string text)
	{
		text = text.Trim();

		switch (key)
		{
			case "weather":
				// two numbers split by a comma are coordinates, anything else is a city name
				var parts = text.Split(',');
				if (parts.Length == 2
					&& float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float lat)
					&& float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lon))
				{
					widget.Latitude = lat;
					widget.Longitude = lon;
					widget.City = null;
				}
				else
				{
					widget.City = text == "" ? null : text;
				}
				break;

			case "stocks":
				widget.Symbols = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.ToUpperInvariant())
					.ToArray();
				break;

			case "news":
				widget.FeedUrl = text == "" ? null : text;
				break;
		}
	}

	// ---------------- tray menu and window stuff ----------------

	private async void RefreshWidgets_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

	// Opens config.json in whatever editor Windows has for .json files.
	// Falls back to Notepad if there isn't one, so it never just fails.
	private async void OpenConfig_Click(object sender, RoutedEventArgs e)
	{
		if (!File.Exists(ConfigPath))
			await Config.LoadOrCreateAsync(ConfigPath);   // writes the defaults if it's missing

		try
		{
			Process.Start(new ProcessStartInfo(ConfigPath) { UseShellExecute = true });
		}
		catch
		{
			Process.Start("notepad.exe", $"\"{ConfigPath}\"");
		}
	}

	private void Window_Loaded(object sender, RoutedEventArgs e)
	{
		// ShowInTaskbar="False" alone can get undone by theme/host changes,
		// so force the tool-window style at the Win32 level too.
		var hwnd = new WindowInteropHelper(this).Handle;
		int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
		NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
			(exStyle | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW);
	}

	// Win+D and "minimise all" set WindowState directly and don't care
	// about the styles above, so bounce it straight back.
	protected override void OnStateChanged(EventArgs e)
	{
		if (WindowState == WindowState.Minimized)
			WindowState = WindowState.Normal;
		base.OnStateChanged(e);
	}
	// Drag the widget by any bare bit of it - the gaps, the text, the card itself.
	// Buttons and text boxes handle their own clicks, so they never reach this.
	private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState == MouseButtonState.Pressed)   // DragMove throws if the button isn't really down
			DragMove();
	}

	private void ToggleVisibility_Click(object sender, RoutedEventArgs e) =>
		Visibility = Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;

	private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
	#endregion
}

// Ties one card in the XAML to its entry in config.json.
// SettingsBox is null for widgets with nothing of their own to set (github).
internal sealed class WidgetSlot
{
	#region Properties
	public string Key { get; }
	public Border Card { get; }
	public TextBlock Data { get; }
	public TextBlock Secondary { get; }
	public ToggleButton EnabledSwitch { get; }
	public TextBox ColourBox { get; }
	public TextBox RateBox { get; }
	public TextBox? SettingsBox { get; }
	public string AccentKey { get; }
	public string SecondaryKey { get; }
	public string BackgroundKey { get; }
	public Func<WidgetConfig, IWidgetSource> Build { get; }
	#endregion
	#region Constructor
	public WidgetSlot(string key, Border card, TextBlock data, TextBlock secondary,
		ToggleButton enabledSwitch, TextBox colourBox, TextBox rateBox, TextBox? settingsBox,
		string accentKey, string secondaryKey, string backgroundKey,
		Func<WidgetConfig, IWidgetSource> build)
	{
		Key = key;
		Card = card;
		Data = data;
		Secondary = secondary;
		EnabledSwitch = enabledSwitch;
		ColourBox = colourBox;
		RateBox = rateBox;
		SettingsBox = settingsBox;
		AccentKey = accentKey;
		SecondaryKey = secondaryKey;
		BackgroundKey = backgroundKey;
		Build = build;
	}
	#endregion
		
}

internal static class NativeMethods
{
	public const int GWL_EXSTYLE = -20;
	public const int WS_EX_TOOLWINDOW = 0x00000080;
	public const int WS_EX_APPWINDOW = 0x00040000;

	[DllImport("user32.dll")]
	public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}