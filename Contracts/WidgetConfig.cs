namespace CS_IA_2027.Contracts;

// Widget configuration class. Used to deserialize the config.json file for each widget.
public class WidgetConfig
{
	#region Properties
	public int UpdateRate { get; set; } = 300;   // seconds

	public string? ParserFile { get; set; }

	public string PrimaryColor { get; set; } = "#3D5AFE"; // default values
	public bool Enabled { get; set; } = true;
	public int Position { get; set; }

	public float? Latitude { get; set; }
	public float? Longitude { get; set; }
	public string[]? Symbols { get; set; }
	public string? FeedUrl { get; set; }
	#endregion
}