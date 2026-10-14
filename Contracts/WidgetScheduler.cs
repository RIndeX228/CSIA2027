using System.Windows.Threading;

namespace CS_IA_2027.Services;

// Holds every widget's timer. StopAll(): timers get rebuilt on every config reload
public sealed class WidgetScheduler
{
	#region Properties
	private readonly List<DispatcherTimer> _timers = new();
	private CancellationTokenSource _cts = new();
	#endregion
	#region Methods
	// Adds a new widget refresh task to the scheduler
	public void Add(TimeSpan interval, Func<CancellationToken, Task> refresh)
	{
		var token = _cts.Token;
		var timer = new DispatcherTimer { Interval = interval };
		timer.Tick += async (_, _) => await Run(refresh, token);
		timer.Start();
		_timers.Add(timer);
		_ = Run(refresh, token);
	}
	// Stops all timers and cancels all running tasks
	public void StopAll()
	{
		foreach (var timer in _timers)
			timer.Stop();

		_timers.Clear();

		_cts.Cancel();
		_cts.Dispose();
		_cts = new CancellationTokenSource();
	}
	// Runs the refresh function and handles cancellation
	private static async Task Run(Func<CancellationToken, Task> refresh, CancellationToken token)
	{
		try
		{
			await refresh(token);
		}
		catch (OperationCanceledException)
		{
			
		}
	}
	#endregion
}