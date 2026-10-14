using System.Text.Json;
using CS_IA_2027.Models;
using CS_IA_2027.Contracts;
using CS_IA_2027.Services;


namespace CS_IA_2027.Scrapers;

public class WeatherScrapper : IWidgetSource
{
	#region Properties
	private readonly string? _apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
	private readonly float _lat;
	private readonly float _lon;

	public string WidgetId => "weather";
	public string DisplayName => "Weather";
	public TimeSpan RefreshInterval => TimeSpan.FromMinutes(15);
	#endregion
	#region Constructors
	public WeatherScrapper(float lat, float lon)
	{
		_lat = lat;
		_lon = lon;
	}
	#endregion
	#region Methods
	public async Task<WidgetResult> RefreshAsync(CancellationToken ct = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
			return WidgetResult.Fail("WEATHER_API_KEY not set");

		var url = $"https://api.openweathermap.org/data/2.5/weather?lat={_lat}&lon={_lon}&appid={_apiKey}&units=metric";
		try
		{
			using var response = await SharedHttp.Client.GetAsync(url, ct);
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
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			return WidgetResult.Fail(ex.Message);
		}
	}

	private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
	#endregion
}