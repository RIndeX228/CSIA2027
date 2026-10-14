namespace CS_IA_2027.Contracts;

public interface IWidgetSource
{
	#region Properties
	string WidgetId { get; }
	string DisplayName { get; }
	#endregion
	TimeSpan RefreshInterval { get; }
	Task<WidgetResult> RefreshAsync(CancellationToken ct = default);

}