using System.Text.Json;
using CS_IA_2027.Models;
using CS_IA_2027.Contracts;
using CS_IA_2027.Services;

namespace CS_IA_2027.Scrapers;

public class StockScrapper : IWidgetSource
{
	#region Properties
	private readonly string? _apiKey = Environment.GetEnvironmentVariable("FINNHUB_API_KEY"); // gets the API key from environment variable. Environment variables are used for safety reasons
	private readonly string[] _symbols;
	public string WidgetId => "stocks";
	public string DisplayName => "Stocks";
	public TimeSpan RefreshInterval => TimeSpan.FromMinutes(2);
	public StockScrapper(params string[] symbols)
	{
		_symbols = symbols.Length > 0 ? symbols : new[] { "AAPL" };
	}
	#endregion
	#region Methods
	public async Task<WidgetResult> RefreshAsync(CancellationToken ct = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
			return WidgetResult.Fail("FINNHUB_API_KEY not set");

		try
		{
			var quotes = new List<StockItem>();
			foreach (var symbol in _symbols)
			{
				var url = $"https://finnhub.io/api/v1/quote?symbol={symbol}&token={_apiKey}";
				using var response = await SharedHttp.Client.GetAsync(url, ct);
				response.EnsureSuccessStatusCode();

				using var stream = await response.Content.ReadAsStreamAsync(ct);
				using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
				var root = doc.RootElement;

				var price = root.GetProperty("c").GetDouble();
				var changePercent = root.GetProperty("dp").GetDouble();
				quotes.Add(new StockItem(symbol, price, changePercent));
			}

			if (quotes.Count == 0)
				return WidgetResult.Fail("No symbols configured");

			var headline = $"{quotes[0].Symbol} {quotes[0].Price:0.##}";
			var secondary = string.Join("  ·  ", quotes.Select(q =>
				$"{q.Symbol} {(q.ChangePercent >= 0 ? "+" : "")}{q.ChangePercent:0.##}%"));
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