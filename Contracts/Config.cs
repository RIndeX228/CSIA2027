using System.IO;
using System.Text.Json;

namespace CS_IA_2027.Contracts;

// Everything here has to be a property - System.Text.Json skips public
// fields unless you turn IncludeFields on, which is how the old version
// silently kept falling back to defaults.
public class Config
{
	// Matches the file: an array of objects, each holding one named widget.
	// A plain object ("widgets": { "weather": {...} }) would be tidier, but
	// this keeps the file looking the way it was designed.
	public List<Dictionary<string, WidgetConfig>> Widgets { get; set; } = new();

	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	// Flattens the array-of-one-key-objects into something usable.
	// Duplicate keys: first one wins.
	public Dictionary<string, WidgetConfig> WidgetMap()
	{
		var map = new Dictionary<string, WidgetConfig>(StringComparer.OrdinalIgnoreCase);

		foreach (var entry in Widgets)
			foreach (var pair in entry)
				map.TryAdd(pair.Key, pair.Value);

		return map;
	}

	// Never throws - a broken config shouldn't take the whole app down,
	// we just fall back to defaults and carry on.
	public static async Task<Config> LoadOrCreateAsync(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				var fresh = Default();
				await fresh.SaveAsync(path);
				return fresh;
			}

			await using FileStream fs = File.OpenRead(path);
			return await JsonSerializer.DeserializeAsync<Config>(fs, Options) ?? Default();
		}
		catch
		{
			return Default();
		}
	}

	public async Task SaveAsync(string path)
	{
		await using FileStream fs = File.Create(path);
		await JsonSerializer.SerializeAsync(fs, this, Options);
	}

	// Used on first run so there's always a file sitting there to edit.
	private static Config Default()
	{
		var cfg = new Config();

		cfg.Add("weather", new WidgetConfig
		{
			UpdateRate = 900,
			PrimaryColor = "#3D5AFE",
			Position = 0,
			Latitude = 52.2297f,
			Longitude = 21.0175f
		});

		cfg.Add("github", new WidgetConfig
		{
			UpdateRate = 300,
			PrimaryColor = "#FF2FA3",
			Position = 1
		});

		cfg.Add("stocks", new WidgetConfig
		{
			UpdateRate = 120,
			PrimaryColor = "#FF8A1E",
			Position = 2,
			Symbols = new[] { "AAPL", "MSFT" }
		});

		cfg.Add("news", new WidgetConfig
		{
			UpdateRate = 600,
			PrimaryColor = "#22D3EE",
			Position = 3,
			FeedUrl = "https://feeds.bbci.co.uk/news/rss.xml"
		});

		return cfg;
	}

	private void Add(string key, WidgetConfig widget) =>
		Widgets.Add(new Dictionary<string, WidgetConfig> { [key] = widget });
}