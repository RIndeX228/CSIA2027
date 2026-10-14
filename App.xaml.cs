using System.Configuration;
using System.Data;
using System.Windows;

namespace CS_IA_2027
{
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			new MainWindow().Show();
		}
	}

}
