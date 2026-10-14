using CS_IA_2027;
using CS_IA_2027.Services;
using CS_IA_2027.Models;
using CS_IA_2027.Contracts;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CS_IA_2027.Scrapers;

public class GitHubNotificationsScrapper : IWidgetSource
{
	#region Properties
	private readonly string? _token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
	public string WidgetId => "github";
	public string DisplayName => "GitHub";
	public TimeSpan RefreshInterval => TimeSpan.FromMinutes(5);
	#endregion
	#region Methods
	public async Task<WidgetResult> RefreshAsync(CancellationToken ct = default)
	{
		if (string.IsNullOrEmpty(_token))
			return WidgetResult.Fail("GITHUB_TOKEN not set");

		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/notifications?all=false");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
			request.Headers.UserAgent.ParseAdd("CS-AI-2027-Widget/1.0");
			request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

			using var response = await SharedHttp.Client.SendAsync(request, ct);
			response.EnsureSuccessStatusCode();

			using var stream = await response.Content.ReadAsStreamAsync(ct);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

			var items = new List<GitHubNotificationItem>();
			foreach (var thread in doc.RootElement.EnumerateArray())
			{
				var repo = thread.GetProperty("repository").GetProperty("full_name").GetString() ?? "";
				var title = thread.GetProperty("subject").GetProperty("title").GetString() ?? "";
				items.Add(new GitHubNotificationItem(repo, title));
			}

			if (items.Count == 0)
				return WidgetResult.Ok("0", "No notifications");

			var preview = string.Join(" · ", items.Take(2).Select(i => i.Title));
			return WidgetResult.Ok(items.Count.ToString(), preview);
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
