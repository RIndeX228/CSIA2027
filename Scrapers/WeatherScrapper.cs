using System.Net;
using System.Text.Json;
using CS_IA_2027.Contracts;
using CS_IA_2027.Models;
using CS_IA_2027.Services;

namespace CS_IA_2027.Scrapers;

public class WeatherScrapper : IWidgetSource
{
	#region Properties
	private readonly string? _apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
	private readonly float _lat;
	private readonly float _lon;
	private readonly string? _city;

	public string WidgetId => "weather";
	public string DisplayName => "Weather";
	public TimeSpan RefreshInterval => TimeSpan.FromMinutes(15);
	#endregion
	#region Constructors

	public WeatherScrapper(float lat, float lon, string? city = null)
	{
		_lat = lat;
		_lon = lon;
		_city = city;
	}

	#endregion
	#region Methods
	public async Task<WidgetResult> RefreshAsync(CancellationToken ct = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
			return WidgetResult.Fail("WEATHER_API_KEY not set");

		// A city name wins over the coordinates when there is one. The coordinates go through
		// FormattableString.Invariant because on a Polish PC a plain {_lat} turns into 52,2297
		// (comma) and the API can't read that.
		string where = string.IsNullOrWhiteSpace(_city)
			? FormattableString.Invariant($"lat={_lat}&lon={_lon}")
			: "q=" + Uri.EscapeDataString(_city);

		var url = $"https://api.openweathermap.org/data/2.5/weather?{where}&appid={_apiKey}&units=metric";

		try
		{
			using var response = await SharedHttp.Client.GetAsync(url, ct);
			if (response.StatusCode == HttpStatusCode.NotFound)
				return WidgetResult.Fail("City not found");
			response.EnsureSuccessStatusCode();

			using var stream = await response.Content.ReadAsStreamAsync(ct);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
			var root = doc.RootElement;

			var temp = root.GetProperty("main").GetProperty("temp").GetDouble();
			var description = root.GetProperty("weather")[0].GetProperty("description").GetString() ?? "";
			var city = root.GetProperty("name").GetString() ?? "Unknown";

			var data = new WeatherItem(city, temp, description);
			return WidgetResult.Ok($"{data.TempC:0.#}°C", $"{Capitalize(data.Description)} · {data.City}");
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;   // config reloaded mid-fetch, not a real failure
		}
		catch (Exception ex)
		{
			return WidgetResult.Fail(ex.Message);
		}
	}

	// OpenWeather sends descriptions lowercase ("light rain").
	private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
	#endregion
}