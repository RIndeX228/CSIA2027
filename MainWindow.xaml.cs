using CS_IA_2027.Contracts;
using CS_IA_2027.Scrapers;
using CS_IA_2027.Services;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CS_IA_2027
{
	public partial class MainWindow : Window
	{
		#region Properties
		private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json"); // Sets the path to the config file in the base directory of the application
		private readonly List<WidgetSlot> _slots;
		private readonly WidgetScheduler _scheduler = new();
		#endregion
		#region Constructors
		public MainWindow()
		{
			InitializeComponent();
			_slots = BuildSlots();
			_ = ReloadAsync();
		}
		#endregion
		#region Methods
		private List<WidgetSlot> BuildSlots() => new()
		{
			new WidgetSlot("weather", WeatherCard, WeatherDataText, WeatherSecondaryText, "WeatherAccentBrush", "WeatherCardBackground", config => new WeatherScrapper(config.Latitude ?? 52.2297f, config.Longitude ?? 21.0175f)),
			new WidgetSlot("github", GitHubCard, GitHubDataText, GitHubSecondaryText, "GithubAccentBrush", "GithubCardBackground", config => new GitHubNotificationsScrapper()),
			new WidgetSlot("stocks", StockCard, StockDataText, StockSecondaryText, "StocksAccentBrush", "StocksCardBackground", config => new StockScrapper(config.Symbols ?? new[] { "AAPL", "MSFT" })),
			new WidgetSlot("news", NewsCard, NewsDataText, NewsSecondaryText, "NewsAccentBrush", "NewsCardBackground", config => new RssScrapper(config.FeedUrl))
		};
		private async Task ReloadAsync()
		{
			var config = await Config.LoadOrCreateAsync(ConfigPath);
			Apply(config);
		}
		private void Apply(Config config)
		{
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
				var interval = TimeSpan.FromSeconds(Math.Max(5, widget.UpdateRate));   // stops updateRate: 0 hammering the APIs
				_scheduler.Add(interval, ct => UpdateCardAsync(source, slot, ct));
			}

			Reorder(map);
		}

		private static async Task RefreshWidget(IWidgetSource source, TextBlock dataText, TextBlock secondaryText)
		{
			var result = await source.RefreshAsync();
			dataText.Text = result.Success ? result.DisplayText : "—";
			secondaryText.Text = result.Success ? (result.SecondaryText ?? "") : result.ErrorMessage ?? "Error";
		}
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
			// Belt-and-suspenders: force WS_EX_TOOLWINDOW at the Win32 level so
			// the window never shows in the taskbar or Alt+Tab, even if
			// ShowInTaskbar gets flipped later (e.g. by a theme/host change).
			var hwnd = new WindowInteropHelper(this).Handle;
			int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
			NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
				(exStyle | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW);
		}

		private static async Task UpdateCardAsync(IWidgetSource source, WidgetSlot slot, CancellationToken ct)
		{
			var result = await source.RefreshAsync(ct);
			slot.Data.Text = result.Success ? result.DisplayText : "—";
			slot.Secondary.Text = result.Success ? result.SecondaryText ?? "" : result.ErrorMessage ?? "Error";
		}

		private static void ApplyColours(WidgetSlot slot, WidgetConfig widget)
		{
			var accent = ParseColour(widget.PrimaryColor, Colors.White);

			var resources = Application.Current.Resources;
			resources[slot.AccentKey] = Frozen(new SolidColorBrush(accent));
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

			// gap between cards, but nothing hanging off the bottom one
			var visible = ordered.Where(s => s.Card.Visibility == Visibility.Visible).ToList();
			for (int i = 0; i < visible.Count; i++)
				visible[i].Card.Margin = new Thickness(0, 0, 0, i == visible.Count - 1 ? 0 : 8);
		}

		private async void RefreshWidgets_Click(object sender, RoutedEventArgs e) => await ReloadAsync();



		// Win+D and "minimise all" set WindowState directly and don't care
		// about the styles above, so bounce it straight back.
		protected override void OnStateChanged(EventArgs e)
		{
			if (WindowState == WindowState.Minimized)
				WindowState = WindowState.Normal;
			base.OnStateChanged(e);
		}


		// Show Desktop (Win+D) and "minimize all" act on WindowState directly,
		// bypassing ShowInTaskbar/WS_EX_TOOLWINDOW entirely. Snap back to
		// Normal immediately so the widget never actually collapses.


		// No title bar, so dragging any part of the widget stack is how the
		// user repositions it.
		private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

		private void ToggleVisibility_Click(object sender, RoutedEventArgs e) =>
			Visibility = Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;

		private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
	}
		#endregion
	internal sealed class WidgetSlot
	{
		#region Properties
		public string Key { get; }
		public Border Card { get; }
		public TextBlock Data { get; }
		public TextBlock Secondary { get; }
		public string AccentKey { get; }
		public string BackgroundKey { get; }
		public Func<WidgetConfig, IWidgetSource> Build { get; }
		#endregion
		#region Constructors
		public WidgetSlot(string key, Border card, TextBlock data, TextBlock secondaryData, string accentKey, string backgroundKey, Func<WidgetConfig, IWidgetSource> build)
		{
			Key = key;
			Card = card;
			Data = data;
			Secondary = secondaryData;
			AccentKey = accentKey;
			BackgroundKey = backgroundKey;
			Build = build;
		}
		#endregion
	}
	// Source: Claude
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
}
