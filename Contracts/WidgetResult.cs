namespace CS_IA_2027.Contracts;

// What every scraper hands back: a headline value, an optional supporting line, and an error message when the fetch didn't work.
public record WidgetResult(bool Success, string DisplayText, string? SecondaryText = null, string? ErrorMessage = null)
{
	public static WidgetResult Ok(string text, string? secondary = null) => new(true, text, secondary);
	public static WidgetResult Fail(string error) => new(false, "—", null, error);
}