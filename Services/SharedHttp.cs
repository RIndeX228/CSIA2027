using System.Net.Http;

namespace CS_IA_2027.Services;

internal static class SharedHttp
{
	public static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
}