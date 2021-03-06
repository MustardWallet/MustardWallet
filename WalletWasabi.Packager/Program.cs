using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubsys;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Packager
{
	public static class Program
	{
#pragma warning disable CS0162 // Unreachable code detected
		// 0. Dump Client version (or else wrong .msi will be created) - Helpers.Constants.ClientVersion
		// 1. Publish with Packager.
		// 2. Build WIX project with Release and x64 configuration.
		// 3. Sign with Packager, set restore true so the password won't be kept.

		public const bool DoPublish = true;
		public const bool DoSign = false;
		public const bool DoRestoreProgramCs = false;

		public const string PfxPath = "C:\\digicert.pfx";
		public const string ExecutableName = "mustardwalletltc";

		// https://docs.microsoft.com/en-us/dotnet/articles/core/rid-catalog
		// BOTTLENECKS:
		// Tor - win-32, linux-32, osx-64
		// .NET Core - win-32, linux-64, osx-64
		// Avalonia - win7-32, linux-64, osx-64
		// We'll only support x64, if someone complains, we can come back to it.
		// For 32 bit Windows there needs to be a lot of WIX configuration to be done.
		public static string[] Targets = new[]
		{
			"win7-x64",
			"linux-x64",
			"osx-x64"
		};

		public static string PackagerProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\"));
		public static string SolutionDirectory = Path.GetFullPath(Path.Combine(PackagerProjectDirectory, "..\\"));
		public static string GuiProjectDirectory = Path.GetFullPath(Path.Combine(SolutionDirectory, "WalletWasabi.Gui\\"));
		public static string LibraryProjectDirectory = Path.GetFullPath(Path.Combine(SolutionDirectory, "WalletWasabi\\"));
		public static string WixProjectDirectory = Path.GetFullPath(Path.Combine(SolutionDirectory, "WalletWasabi.WindowsInstaller\\"));
		public static string BinDistDirectory = Path.GetFullPath(Path.Combine(GuiProjectDirectory, "bin\\dist"));

		public static string VersionPrefix = Constants.ClientVersion.Revision == 0 ? Constants.ClientVersion.ToString(3) : Constants.ClientVersion.ToString();

		public static bool OnlyBinaries;
		public static bool OnlyCreateDigests;

		private static void Main(string[] args)
		{
			if (MacSignTools.IsMacSignMode())
			{
				MacSignTools.Sign();
				return;
			}

			// If I want a list of up to date onions run it with '--getonions'.
			if (IsGetOnionsMode(args))
			{
				GetOnions();
				return;
			}

			// If I want a list of up to date onions run it with '--getonions'.
			if (IsReduceOnionsMode(args))
			{
				ReduceOnions();
				return;
			}

			// Start with digest creation and return if only digest creation.
			CreateDigests();

			OnlyCreateDigests = IsOnlyCreateDigestsMode(args);
			if (OnlyCreateDigests)
			{
				return;
			}

			// Only binaries mode is for deterministic builds.
			OnlyBinaries = IsOnlyBinariesMode(args);
			ReportStatus();

			if (DoPublish || OnlyBinaries)
			{
				Publish();

				IoHelpers.OpenFolderInFileExplorer(BinDistDirectory);
			}

			if (!OnlyBinaries)
			{
				if (DoSign)
				{
					Sign();
				}

				if (DoRestoreProgramCs)
				{
					RestoreProgramCs();
				}
			}
		}

		private static void GetOnions()
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://bitnodes.21.co/api/v1/");

			using var response = httpClient.GetAsync("snapshots/latest/", HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new HttpRequestException(response.StatusCode.ToString());
			}

			var responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			var json = (JObject)JsonConvert.DeserializeObject(responseString);
			var onions = new List<string>();
			foreach (JProperty node in json["nodes"])
			{
				if (!node.Name.Contains(".onion"))
				{
					continue;
				}

				var userAgent = ((JArray)node.Value)[1].ToString();

				try
				{
					var verString = userAgent.Substring(userAgent.IndexOf("Satoshi:") + 8, 4);
					var ver = new Version(verString);

					if (ver >= new Version("0.16"))
					{
						onions.Add(node.Name);
					}
				}
				catch
				{
				}
			}

			foreach (var onion in onions.OrderBy(x => x))
			{
				Console.WriteLine(onion);
			}
		}

		private static void ReduceOnions()
		{
			var onionFile = Path.Combine(LibraryProjectDirectory, "OnionSeeds", "ltc-mainOnionSeeds.txt");
			var currentOnions = File.ReadAllLines(onionFile).ToHashSet();
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://bitnodes.21.co/api/v1/");

			using var response = httpClient.GetAsync("snapshots/latest/", HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new HttpRequestException(response.StatusCode.ToString());
			}

			var responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			var json = (JObject)JsonConvert.DeserializeObject(responseString);
			var onions = new List<string>();
			foreach (JProperty node in json["nodes"])
			{
				if (!node.Name.Contains(".onion"))
				{
					continue;
				}

				var userAgent = ((JArray)node.Value)[1].ToString();

				try
				{
					var verString = userAgent.Substring(userAgent.IndexOf("Satoshi:") + 8, 4);
					var ver = new Version(verString);

					if (ver >= new Version("0.16") && currentOnions.Contains(node.Name))
					{
						onions.Add(node.Name);
					}
				}
				catch
				{
				}
			}

			foreach (var onion in onions.OrderBy(x => x))
			{
				Console.WriteLine(onion);
			}
		}

		private static void CreateDigests()
		{
			var tempDir = "DigestTempDir";
			IoHelpers.DeleteRecursivelyWithMagicDustAsync(tempDir).GetAwaiter().GetResult();
			Directory.CreateDirectory(tempDir);

			var torDaemonsDir = Path.Combine(LibraryProjectDirectory, "TorDaemons");
			string torWinZip = Path.Combine(torDaemonsDir, "tor-win32.zip");
			IoHelpers.BetterExtractZipToDirectoryAsync(torWinZip, tempDir).GetAwaiter().GetResult();
			File.Move(Path.Combine(tempDir, "Tor", "tor.exe"), Path.Combine(tempDir, "TorWin"));

			string torLinuxZip = Path.Combine(torDaemonsDir, "tor-linux64.zip");
			IoHelpers.BetterExtractZipToDirectoryAsync(torLinuxZip, tempDir).GetAwaiter().GetResult();
			File.Move(Path.Combine(tempDir, "Tor", "tor"), Path.Combine(tempDir, "TorLin"));

			string torOsxZip = Path.Combine(torDaemonsDir, "tor-osx64.zip");
			IoHelpers.BetterExtractZipToDirectoryAsync(torOsxZip, tempDir).GetAwaiter().GetResult();
			File.Move(Path.Combine(tempDir, "Tor", "tor"), Path.Combine(tempDir, "TorOsx"));

			var tempDirInfo = new DirectoryInfo(tempDir);
			var binaries = tempDirInfo.GetFiles();
			Console.WriteLine("Digests:");
			foreach (var file in binaries)
			{
				var filePath = file.FullName;
				var hash = ByteHelpers.ToHex(IoHelpers.GetHashFile(filePath)).ToLowerInvariant();
				Console.WriteLine($"{file.Name}: {hash}");
			}

			IoHelpers.DeleteRecursivelyWithMagicDustAsync(tempDir).GetAwaiter().GetResult();
		}

		private static void ReportStatus()
		{
			if (OnlyBinaries)
			{
				Console.WriteLine($"I'll only generate binaries and disregard all other options.");
			}
			Console.WriteLine($"{nameof(VersionPrefix)}:\t\t\t{VersionPrefix}");
			Console.WriteLine($"{nameof(ExecutableName)}:\t\t\t{ExecutableName}");

			Console.WriteLine();
			Console.Write($"{nameof(Targets)}:\t\t\t");
			foreach (var target in Targets)
			{
				if (Targets.Last() != target)
				{
					Console.Write($"{target}, ");
				}
				else
				{
					Console.Write(target);
				}
			}
			Console.WriteLine();
		}

		private static bool IsOnlyBinariesMode(string[] args)
		{
			bool onlyBinaries = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("onlybinaries", StringComparison.OrdinalIgnoreCase))
					{
						onlyBinaries = true;
						break;
					}
				}
			}

			return onlyBinaries;
		}

		private static bool IsOnlyCreateDigestsMode(string[] args)
		{
			bool onlyCreateDigests = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("onlycreatedigests", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("onlycreatedigest", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("onlydigests", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("onlydigest", StringComparison.OrdinalIgnoreCase))
					{
						onlyCreateDigests = true;
						break;
					}
				}
			}

			return onlyCreateDigests;
		}

		private static bool IsGetOnionsMode(string[] args)
		{
			bool getOnions = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("getonions", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("getonion", StringComparison.OrdinalIgnoreCase))
					{
						getOnions = true;
						break;
					}
				}
			}

			return getOnions;
		}

		private static bool IsReduceOnionsMode(string[] args)
		{
			bool getOnions = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("reduceonions", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("reduceonion", StringComparison.OrdinalIgnoreCase))
					{
						getOnions = true;
						break;
					}
				}
			}

			return getOnions;
		}

		private static void RestoreProgramCs()
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = "cmd",
				RedirectStandardInput = true,
				WorkingDirectory = PackagerProjectDirectory
			});
			process.StandardInput.WriteLine($"git checkout -- Program.cs && exit");
			process.WaitForExit();
		}

		private static void Sign()
		{
			foreach (string target in Targets)
			{
				if (target.StartsWith("win", StringComparison.OrdinalIgnoreCase))
				{
					string publishedFolder = Path.Combine(BinDistDirectory, target);

					Console.WriteLine("Move created .msi");
					var msiPath = Path.Combine(WixProjectDirectory, @"bin\Release\MustardWalletLTC.msi");
					if (!File.Exists(msiPath))
					{
						throw new Exception(".msi does not exist. Expected path: MustardWalletLTC.msi.");
					}
					var msiFileName = Path.GetFileNameWithoutExtension(msiPath);
					var newMsiPath = Path.Combine(BinDistDirectory, $"{msiFileName}-{VersionPrefix}.msi");
					File.Move(msiPath, newMsiPath);

					Console.Write("Enter Code Signing Certificate Password: ");
					string pfxPassword = PasswordConsole.ReadPassword();
					// Sign code with digicert.
					using (var process = Process.Start(new ProcessStartInfo
					{
						FileName = "cmd",
						RedirectStandardInput = true,
						WorkingDirectory = BinDistDirectory
					}))
					{
						process.StandardInput.WriteLine($"signtool sign /d \"Mustard Wallet for Litecoin\" /f \"{PfxPath}\" /p {pfxPassword} /t http://timestamp.digicert.com /a \"{newMsiPath}\" && exit");
						process.WaitForExit();
					}

					IoHelpers.DeleteRecursivelyWithMagicDustAsync(publishedFolder).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {publishedFolder}");
				}
				else if (target.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
				{
					string dmgFilePath = Path.Combine(BinDistDirectory, $"MustardWalletLTC-{VersionPrefix}.dmg");
					if (!File.Exists(dmgFilePath))
					{
						throw new Exception(".dmg does not exist.");
					}
					string zipFilePath = Path.Combine(BinDistDirectory, $"MustardWalletLTC-osx-{VersionPrefix}.zip");
					if (File.Exists(zipFilePath))
					{
						File.Delete(zipFilePath);
					}
				}
			}

			Console.WriteLine("Signing final files...");
			var finalFiles = Directory.GetFiles(BinDistDirectory);

			foreach (var finalFile in finalFiles)
			{
				using (var process = Process.Start(new ProcessStartInfo
				{
					FileName = "cmd",
					RedirectStandardInput = true,
					WorkingDirectory = BinDistDirectory
				}))
				{
					process.StandardInput.WriteLine($"gpg --armor --detach-sign {finalFile} && exit");
					process.WaitForExit();
				}

				using (var process = Process.Start(new ProcessStartInfo
				{
					FileName = "cmd",
					RedirectStandardInput = true,
					WorkingDirectory = WixProjectDirectory
				}))
				{
					process.StandardInput.WriteLine($"git checkout -- ComponentsGenerated.wxs && exit");
					process.WaitForExit();
				}
			}

			IoHelpers.OpenFolderInFileExplorer(BinDistDirectory);
		}

		private static void Publish()
		{
			if (Directory.Exists(BinDistDirectory))
			{
				IoHelpers.DeleteRecursivelyWithMagicDustAsync(BinDistDirectory).GetAwaiter().GetResult();
				Console.WriteLine($"Deleted {BinDistDirectory}");
			}

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "cmd",
				RedirectStandardInput = true,
				WorkingDirectory = GuiProjectDirectory
			}))
			{
				process.StandardInput.WriteLine("dotnet clean --configuration Release && exit");
				process.WaitForExit();
			}

			var guiBinReleaseDirectory = Path.GetFullPath(Path.Combine(GuiProjectDirectory, "bin\\Release"));
			var libraryBinReleaseDirectory = Path.GetFullPath(Path.Combine(LibraryProjectDirectory, "bin\\Release"));
			if (Directory.Exists(guiBinReleaseDirectory))
			{
				IoHelpers.DeleteRecursivelyWithMagicDustAsync(guiBinReleaseDirectory).GetAwaiter().GetResult();
				Console.WriteLine($"Deleted {guiBinReleaseDirectory}");
			}
			if (Directory.Exists(libraryBinReleaseDirectory))
			{
				IoHelpers.DeleteRecursivelyWithMagicDustAsync(libraryBinReleaseDirectory).GetAwaiter().GetResult();
				Console.WriteLine($"Deleted {libraryBinReleaseDirectory}");
			}

			foreach (string target in Targets)
			{
				string publishedFolder = Path.Combine(BinDistDirectory, target);
				string currentBinDistDirectory = publishedFolder;

				Console.WriteLine();
				Console.WriteLine($"{nameof(currentBinDistDirectory)}:\t{currentBinDistDirectory}");

				Console.WriteLine();
				if (!Directory.Exists(currentBinDistDirectory))
				{
					Directory.CreateDirectory(currentBinDistDirectory);
					Console.WriteLine($"Created {currentBinDistDirectory}");
				}

				using (var process = Process.Start(new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = $"clean",
					WorkingDirectory = GuiProjectDirectory
				}))
				{
					process.WaitForExit();
				}

				// https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish?tabs=netcore21
				// -c|--configuration {Debug|Release}
				//		Defines the build configuration. The default value is Debug.
				// --force
				//		Forces all dependencies to be resolved even if the last restore was successful. Specifying this flag is the same as deleting the project.assets.json file.
				// -o|--output <OUTPUT_DIRECTORY>
				//		Specifies the path for the output directory.
				//		If not specified, it defaults to ./bin/[configuration]/[framework]/publish/ for a framework-dependent deployment or
				//		./bin/[configuration]/[framework]/[runtime]/publish/ for a self-contained deployment.
				//		If the path is relative, the output directory generated is relative to the project file location, not to the current working directory.
				// --self-contained
				//		Publishes the .NET Core runtime with your application so the runtime does not need to be installed on the target machine.
				//		If a runtime identifier is specified, its default value is true. For more information about the different deployment types, see .NET Core application deployment.
				// -r|--runtime <RUNTIME_IDENTIFIER>
				//		Publishes the application for a given runtime. This is used when creating a self-contained deployment (SCD).
				//		For a list of Runtime Identifiers (RIDs), see the RID catalog. Default is to publish a framework-dependent deployment (FDD).
				// --version-suffix <VERSION_SUFFIX>
				//		Defines the version suffix to replace the asterisk (*) in the version field of the project file.
				// https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-restore?tabs=netcore2x
				// --disable-parallel
				//		Disables restoring multiple projects in parallel.
				// --no-cache
				//		Specifies to not cache packages and HTTP requests.
				// https://github.com/dotnet/docs/issues/7568
				// /p:Version=1.2.3.4
				//		"dotnet publish" supports msbuild command line options like /p:Version=1.2.3.4
				using (var process = Process.Start(new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = $"publish --configuration Release --force --output \"{currentBinDistDirectory}\" --self-contained true --runtime \"{target}\" /p:VersionPrefix={VersionPrefix} --disable-parallel --no-cache /p:DebugType=none /p:DebugSymbols=false /p:ErrorReport=none /p:DocumentationFile=\"\" /p:Deterministic=true",
					WorkingDirectory = GuiProjectDirectory
				}))
				{
					process.WaitForExit();
				}

				Tools.ClearSha512Tags(currentBinDistDirectory);

				// Remove Tor binaries that are not relevant to the platform.
				var torFolder = new DirectoryInfo(Path.Combine(currentBinDistDirectory, "TorDaemons"));
				var toNotRemove = "";
				if (target.StartsWith("win"))
				{
					toNotRemove = "win";
				}
				else if (target.StartsWith("linux"))
				{
					toNotRemove = "lin";
				}
				else if (target.StartsWith("osx"))
				{
					toNotRemove = "osx";
				}

				foreach (var file in torFolder.EnumerateFiles())
				{
					if (!file.Name.Contains("data", StringComparison.OrdinalIgnoreCase) && !file.Name.Contains(toNotRemove, StringComparison.OrdinalIgnoreCase))
					{
						File.Delete(file.FullName);
					}
				}

				// Remove binaries that are not relevant to the platform.
				var binaryFolder = new DirectoryInfo(Path.Combine(currentBinDistDirectory, "Microservices", "Binaries"));

				foreach (var dir in binaryFolder.EnumerateDirectories())
				{
					if (!dir.Name.Contains(toNotRemove, StringComparison.OrdinalIgnoreCase))
					{
						IoHelpers.DeleteRecursivelyWithMagicDustAsync(dir.FullName).GetAwaiter().GetResult();
					}
				}

				// Rename the final exe.
				string oldExecutablePath;
				string newExecutablePath;
				if (target.StartsWith("win"))
				{
					oldExecutablePath = Path.Combine(currentBinDistDirectory, "WalletWasabi.Gui.exe");
					newExecutablePath = Path.Combine(currentBinDistDirectory, $"{ExecutableName}.exe");
				}
				else // Linux & OSX
				{
					oldExecutablePath = Path.Combine(currentBinDistDirectory, "WalletWasabi.Gui");
					newExecutablePath = Path.Combine(currentBinDistDirectory, ExecutableName);
				}
				File.Move(oldExecutablePath, newExecutablePath);

				long installedSizeKb = Tools.DirSize(new DirectoryInfo(publishedFolder)) / 1000;

				if (target.StartsWith("win"))
				{
					var icoPath = Path.Combine(GuiProjectDirectory, "Assets", "MustardLogo.ico");
					using (var process = Process.Start(new ProcessStartInfo
					{
						FileName = "rcedit", // https://github.com/electron/rcedit/
						Arguments = $"\"{newExecutablePath}\" --set-icon \"{icoPath}\" --set-file-version \"{VersionPrefix}\" --set-product-version \"{VersionPrefix}\" --set-version-string \"LegalCopyright\" \"MIT\" --set-version-string \"CompanyName\" \"MustardWallet\" --set-version-string \"FileDescription\" \"Privacy focused, ZeroLink compliant Litecoin wallet. Based on Wasabi Wallet for Bitcoin .\" --set-version-string \"ProductName\" \"Mustard Wallet for Litecoin\"",
						WorkingDirectory = currentBinDistDirectory
					}))
					{
						process.WaitForExit();
					}

					var daemonExePath = newExecutablePath[0..^4] + "d.exe";
					File.Copy(newExecutablePath, daemonExePath);

					// Do not open console.
					if (!NSubsysUtil.ProcessFile(newExecutablePath))
					{
						Console.WriteLine("ERROR: Could not remove console from exe.");
					}

					// IF IT'S IN ONLYBINARIES MODE DON'T DO ANYTHING FANCY PACKAGING AFTER THIS!!!
					if (OnlyBinaries)
					{
						continue; // In Windows build at this moment it does not matter though.
					}
				}
				else if (target.StartsWith("osx"))
				{
					// IF IT'S IN ONLYBINARIES MODE DON'T DO ANYTHING FANCY PACKAGING AFTER THIS!!!
					if (OnlyBinaries)
					{
						continue;
					}

					ZipFile.CreateFromDirectory(currentBinDistDirectory, Path.Combine(BinDistDirectory, $"MustardWalletLTC-osx-{VersionPrefix}.zip"));

					IoHelpers.DeleteRecursivelyWithMagicDustAsync(currentBinDistDirectory).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {currentBinDistDirectory}");
				}
				else if (target.StartsWith("linux"))
				{
					// IF IT'S IN ONLYBINARIES MODE DON'T DO ANYTHING FANCY PACKAGING AFTER THIS!!!
					if (OnlyBinaries)
					{
						continue;
					}

					Console.WriteLine("Create Linux .tar.gz");
					if (!Directory.Exists(publishedFolder))
					{
						throw new Exception($"{publishedFolder} does not exist.");
					}
					var newFolderName = $"MustardWalletLTC-linux-{VersionPrefix}";
					var newFolderPath = Path.Combine(BinDistDirectory, newFolderName);
					Directory.Move(publishedFolder, newFolderPath);
					publishedFolder = newFolderPath;

					var linuxPath = $"/mnt/c/{Tools.LinuxPath(BinDistDirectory.Replace("C:\\", ""))}"; // We assume that it is on drive C:\.

					var commands = new[]
					{
						"cd ~",
						"sudo umount /mnt/c",
						"sudo mount -t drvfs C: /mnt/c -o metadata",
						$"cd {linuxPath}",
						$"sudo find ./{newFolderName} -type f -exec chmod 644 {{}} \\;",
						$"sudo find ./{newFolderName} -type f \\( -name 'mustardwalletltc' -o -name 'hwi' -o -name 'litecoind' \\) -exec chmod +x {{}} \\;",
						$"tar -pczvf {newFolderName}.tar.gz {newFolderName}"
					};
					string arguments = string.Join(" && ", commands);

					using (var process = Process.Start(new ProcessStartInfo
					{
						FileName = "wsl",
						Arguments = arguments,
						RedirectStandardInput = true,
						WorkingDirectory = BinDistDirectory
					}))
					{
						process.WaitForExit();
					}

					Console.WriteLine("Create Linux .deb");

					var debFolderRelativePath = "deb";
					var debFolderPath = Path.Combine(BinDistDirectory, debFolderRelativePath);
					var linuxUsrLocalBinFolder = "/usr/local/bin/";
					var debUsrLocalBinFolderRelativePath = Path.Combine(debFolderRelativePath, "usr", "local", "bin");
					var debUsrLocalBinFolderPath = Path.Combine(BinDistDirectory, debUsrLocalBinFolderRelativePath);
					Directory.CreateDirectory(debUsrLocalBinFolderPath);
					var debUsrAppFolderRelativePath = Path.Combine(debFolderRelativePath, "usr", "share", "applications");
					var debUsrAppFolderPath = Path.Combine(BinDistDirectory, debUsrAppFolderRelativePath);
					Directory.CreateDirectory(debUsrAppFolderPath);
					var debUsrShareIconsFolderRelativePath = Path.Combine(debFolderRelativePath, "usr", "share", "icons", "hicolor");
					var debUsrShareIconsFolderPath = Path.Combine(BinDistDirectory, debUsrShareIconsFolderRelativePath);
					var debianFolderRelativePath = Path.Combine(debFolderRelativePath, "DEBIAN");
					var debianFolderPath = Path.Combine(BinDistDirectory, debianFolderRelativePath);
					Directory.CreateDirectory(debianFolderPath);
					// If we build the Linux release on Windows , then MustardWalletLTC == mustardwalletltc , which
					// causes a conflict between file and folder name of /usr/local/bin/mustardwalletltc
					// That's why the folder name in the .deb build is /usr/local/bin/MustardWallet-LTC :
					newFolderName = "MustardWallet-LTC";
					var linuxWasabiWalletFolder = Tools.LinuxPathCombine(linuxUsrLocalBinFolder, newFolderName);
					var newFolderRelativePath = Path.Combine(debUsrLocalBinFolderRelativePath, newFolderName);
					newFolderPath = Path.Combine(BinDistDirectory, newFolderRelativePath);
					Directory.Move(publishedFolder, newFolderPath);

					var assetsFolder = Path.Combine(GuiProjectDirectory, "Assets");
					var assetsInfo = new DirectoryInfo(assetsFolder);

					foreach (var file in assetsInfo.EnumerateFiles())
					{
						var number = file.Name.Split(new string[] { "MustardLogo", ".png" }, StringSplitOptions.RemoveEmptyEntries);
						if (number.Length == 1 && int.TryParse(number.First(), out int size))
						{
							string destFolder = Path.Combine(debUsrShareIconsFolderPath, $"{size}x{size}", "apps");
							Directory.CreateDirectory(destFolder);
							file.CopyTo(Path.Combine(destFolder, $"{ExecutableName}.png"));
						}
					}

					var controlFilePath = Path.Combine(debianFolderPath, "control");
					// License format does not yet work, but should work in the future, it's work in progress: https://bugs.launchpad.net/ubuntu/+source/software-center/+bug/435183
					var controlFileContent = $"Package: {ExecutableName}\n" +
						$"Priority: optional\n" +
						$"Section: utils\n" +
						$"Maintainer: Gabriel Krieger <gabrielkrieger@protonmail.com>\n" +
						$"Version: {VersionPrefix}\n" +
						$"Homepage: http://MustardWallet.com\n" +
						$"Vcs-Git: git://github.com/MustardWallet/MustardWalletLTC.git\n" +
						$"Vcs-Browser: https://github.com/MustardWallet/MustardWalletLTC\n" +
						$"Architecture: amd64\n" +
						$"License: Open Source (MIT)\n" +
						$"Installed-Size: {installedSizeKb}\n" +
						$"Description: open-source, non-custodial, privacy focused Litecoin wallet, based on Wasabi Wallet for Bitcoin .\n" +
						$"  Built-in Tor, CoinJoin and Coin Control features.\n" +
						$"  Based on Wasabi Wallet for Bitcoin .\n";

					File.WriteAllText(controlFilePath, controlFileContent, Encoding.ASCII);

					var desktopFilePath = Path.Combine(debUsrAppFolderPath, $"{ExecutableName}.desktop");
					var desktopFileContent = $"[Desktop Entry]\n" +
						$"Type=Application\n" +
						$"Name=Mustard Wallet for Litecoin\n" +
						$"StartupWMClass=Mustard Wallet for Litecoin\n" +
						$"GenericName=Litecoin Wallet\n" +
						$"Comment=Privacy focused Litecoin wallet. Based on Wasabi Wallet for Bitcoin .\n" +
						$"Icon={ExecutableName}\n" +
						$"Terminal=false\n" +
						$"Exec={ExecutableName}\n" +
						$"Categories=Office;Finance;\n" +
						$"Keywords=litecoin;wallet;crypto;blockchain;mustard;wasabi;privacy;anon;\n";

					File.WriteAllText(desktopFilePath, desktopFileContent, Encoding.ASCII);

					var wasabiStarterScriptPath = Path.Combine(debUsrLocalBinFolderPath, $"{ExecutableName}");
					var wasabiStarterScriptContent = $"#!/bin/sh\n" +
						$"{ linuxWasabiWalletFolder.TrimEnd('/')}/{ExecutableName} $@\n";

					File.WriteAllText(wasabiStarterScriptPath, wasabiStarterScriptContent, Encoding.ASCII);

					string debExeLinuxPath = Tools.LinuxPathCombine(newFolderRelativePath, ExecutableName);
					string debDestopFileLinuxPath = Tools.LinuxPathCombine(debUsrAppFolderRelativePath, $"{ExecutableName}.desktop");

					commands = new[]
					{
						"cd ~",
						"sudo umount /mnt/c",
						"sudo mount -t drvfs C: /mnt/c -o metadata",
						$"cd {linuxPath}",
						$"sudo find {Tools.LinuxPath(newFolderRelativePath)} -type f -exec chmod 644 {{}} \\;",
						$"sudo find {Tools.LinuxPath(newFolderRelativePath)} -type f \\( -name 'mustardwalletltc' -o -name 'hwi' -o -name 'litecoind' \\) -exec chmod +x {{}} \\;",
						$"sudo chmod -R 0775 {Tools.LinuxPath(debianFolderRelativePath)}",
						$"sudo chmod -R 0644 {debDestopFileLinuxPath}",
						$"dpkg --build {Tools.LinuxPath(debFolderRelativePath)} $(pwd)"
					};
					arguments = string.Join(" && ", commands);
					using (var process = Process.Start(new ProcessStartInfo
					{
						FileName = "wsl",
						Arguments = arguments,
						RedirectStandardInput = true,
						WorkingDirectory = BinDistDirectory
					}))
					{
						process.WaitForExit();
					}

					IoHelpers.DeleteRecursivelyWithMagicDustAsync(debFolderPath).GetAwaiter().GetResult();

					string oldDeb = Path.Combine(BinDistDirectory, $"{ExecutableName}_{VersionPrefix}_amd64.deb");
					string newDeb = Path.Combine(BinDistDirectory, $"MustardWalletLTC-{VersionPrefix}.deb");
					File.Move(oldDeb, newDeb);

					IoHelpers.DeleteRecursivelyWithMagicDustAsync(publishedFolder).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {publishedFolder}");
				}
			}
		}

#pragma warning restore CS0162 // Unreachable code detected
	}
}
