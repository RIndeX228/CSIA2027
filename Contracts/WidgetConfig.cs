namespace CS_IA_2027.Contracts;

// One entry under "widgets" in config.json. The extras at the bottom only
// apply to some widgets - weather ignores Symbols, stocks ignores FeedUrl
// and so on.
public class WidgetConfig
{
	// Warsaw, used when a weather entry has no location at all
	public const float DefaultLatitude = 52.2297f;
	public const float DefaultLongitude = 21.0175f;

	public int UpdateRate { get; set; } = 300;   // seconds

	// Kept because it's in the config format, but nothing loads it.
	// Running a .cs file at runtime would need Roslyn compiled in; the
	// widget's key in config.json is what actually picks the scraper.
	public string? ParserFile { get; set; }

	public string PrimaryColor { get; set; } = "#3D5AFE";
	public string SecondaryColor { get; set; } = "#AEB6C2";
	public bool Enabled { get; set; } = true;
	public int Position { get; set; }

	public float? Latitude { get; set; }
	public float? Longitude { get; set; }
	public string? City { get; set; }   // when this is set it wins over the coordinates
	public string[]? Symbols { get; set; }
	public string? FeedUrl { get; set; }
}