using Mono.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.CommandLine
{
	public class CommandInterpreter
	{
		public CommandInterpreter(TextWriter outW, TextWriter errorW)
		{
			Out = outW;
			Error = errorW;
		}

		private TextWriter Out { get; }
		private TextWriter Error { get; }

		/// <returns>If the GUI should run or not.</returns>
		public async Task<bool> ExecuteCommandsAsync(string[] args, Command mixerCommand, Command passwordFinderCommand, Command crashReportedCommand)
		{
			var showHelp = false;
			var showVersion = false;

			if (args.Length == 0)
			{
				return true;
			}

			var suite = new CommandSet("mustardwalletltc", Out, Error)
			{
				"Usage: mustardwalletltc [OPTIONS]+",
				"Launches Mustard Wallet for Litecoin , based on Wasabi Wallet for Bitcoin .",
				"",
				{ "h|help", "Displays help page and exit.", x => showHelp = x != null },
				{ "v|version", "Displays version and exit.", x => showVersion = x != null },
				"",
				"Available commands are:",
				"",
				mixerCommand,
				passwordFinderCommand,
				crashReportedCommand
			};

			EnsureBackwardCompatibilityWithOldParameters(ref args);
			if (await suite.RunAsync(args) == 0)
			{
				return false;
			}
			if (showHelp)
			{
				ShowVersion();
				await suite.RunAsync(new string[] { "--help" });
				return false;
			}
			else if (showVersion)
			{
				ShowVersion();
				return false;
			}

			return false;
		}

		private static void EnsureBackwardCompatibilityWithOldParameters(ref string[] args)
		{
			var listArgs = args.ToList();
			if (listArgs.Remove("--mix") || listArgs.Remove("-m"))
			{
				listArgs.Insert(0, "mix");
			}
			args = listArgs.ToArray();
		}

		private void ShowVersion()
		{
			Out.WriteLine($"Mustard Wallet for Litecoin Client Version: {Constants.ClientVersion}");
			Out.WriteLine($"Compatible Coordinator Version: {Constants.ClientSupportBackendVersionText}");
			Out.WriteLine($"Compatible Litecoin Core Version: {Constants.BitcoinCoreVersion}");
			Out.WriteLine($"Compatible Hardware Wallet Interface Version: {Constants.HwiVersion}");
		}
	}
}
