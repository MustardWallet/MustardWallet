using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Backend
{
	public class WebsiteTorifier
	{
		public WebsiteTorifier(string rootFolder)
		{
			RootFolder = rootFolder;
			UnversionedFolder = Path.GetFullPath(Path.Combine(RootFolder, "unversioned"));
		}

		public string RootFolder { get; }
		public string UnversionedFolder { get; }

		public async Task CloneAndUpdateOnionIndexHtmlAsync()
		{
			var path = Path.Combine(RootFolder, "index.html");
			var onionPath = Path.Combine(RootFolder, "onion-index.html");

			var content = await File.ReadAllTextAsync(path);

			content = content.Replace("http://p5d45yv2wobyqaj7.onion", "https://MustardWallet.com", StringComparison.Ordinal);
			content = content.Replace("https://blockstream.info", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion", StringComparison.Ordinal);
			content = content.Replace("https://MustardWallet.com/swagger/", "http://p5d45yv2wobyqaj7.onion/swagger/", StringComparison.Ordinal);

			await File.WriteAllTextAsync(onionPath, content);
		}
	}
}
