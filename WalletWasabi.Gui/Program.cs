using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Threading;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using Splat;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static Global Global;

		private static void Main(string[] args)
		{
			bool runGui = false;
			try
			{
				Global = new Global();

				Locator.CurrentMutable.RegisterConstant(Global);

				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				runGui = CommandInterpreter.ExecuteCommandsAsync(Global, args).Result;
				
				if (!runGui)
				{
					return;
				}

				if (Global.InitializeUiConfigAsync().Result)
				{
					Logger.LogInfo($"{nameof(Global.UiConfig)} is successfully initialized.");
				}
				else
				{
					Logger.LogError($"Failed to initialize {nameof(Global.UiConfig)}.");
					return;
				}
				
				Logger.LogSoftwareStarted("Wasabi GUI");

				BuildAvaloniaApp().StartShellApp("Wasabi Wallet", AppMainAsync, args);
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
				throw;
			}
			finally
			{
				MainWindowViewModel.Instance?.Dispose();
				Global.DisposeAsync().Wait();
				AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

				if (runGui)
				{
					Logger.LogSoftwareStopped("Wasabi GUI");
				}
			}
		}

		private static async void AppMainAsync(string[] args)
		{
			AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);
			MainWindowViewModel.Instance = new MainWindowViewModel();

			await Global.InitializeNoWalletAsync();

			MainWindowViewModel.Instance.Initialize();

			Dispatcher.UIThread.Post(GC.Collect);
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception);
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception);
		}

		private static AppBuilder BuildAvaloniaApp()
		{
			bool useGpuLinux = true;

			var result = AppBuilder.Configure<App>();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result
					.UseWin32()
					.UseSkia();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				if (Helpers.Utils.DetectLLVMPipeRasterizer())
				{
					useGpuLinux = false;
				}

				result.UsePlatformDetect()
					.UseManagedSystemDialogs<AppBuilder, WasabiWindow>();
			}
			else
			{
				result.UsePlatformDetect();
			}

			return result
				.With(new Win32PlatformOptions { AllowEglInitialization = true, UseDeferredRendering = true })
				.With(new X11PlatformOptions { UseGpu = useGpuLinux, WmClass = "Wasabi Wallet" })
				.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = true })
				.With(new MacOSPlatformOptions { ShowInDock = true });
		}
	}
}
