using CS_IA_2027;
using CS_IA_2027.Models;
using CS_IA_2027.Services;
using CS_IA_2027.Contracts;
using System.Xml.Linq;


namespace CS_IA_2027.Scrapers;

public class RssScrapper : IWidgetSource
{
	#region Properties
	private const string DefaultFeed = "https://feeds.bbci.co.uk/news/rss.xml";
	private readonly string _feedUrl;
	public string WidgetId => "news";
	public string DisplayName => "News";
	public TimeSpan RefreshInterval => TimeSpan.FromMinutes(10);
	#endregion
	#region Contructors
	public RssScrapper(string? feedUrl = null)
	{
		_feedUrl = feedUrl ?? DefaultFeed;
	}
	#endregion
	#region Methods
	public async Task<WidgetResult> RefreshAsync(CancellationToken ct = default)
	{
		try
		{
			using var response = await SharedHttp.Client.GetAsync(_feedUrl, ct);
			response.EnsureSuccessStatusCode();

			using var stream = await response.Content.ReadAsStreamAsync(ct);
			var doc = XDocument.Load(stream);

			var items = doc.Descendants("item")
				.Select(item => new NewsItem(
					item.Element("title")?.Value ?? "Untitled",
					item.Element("link")?.Value ?? "",
					item.Element("pubDate")?.Value ?? ""))
				.Take(3)
				.ToList();

			if (items.Count == 0)
				return WidgetResult.Fail("No items in feed");

			var headline = items[0].Title;
			var secondary = string.Join(" · ", items.Skip(1).Select(i => i.Title));
			return WidgetResult.Ok(headline, secondary);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			return WidgetResult.Fail(ex.Message);
		}
	}
	#endregion
}