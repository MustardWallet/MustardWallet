using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Configuration
{
	public static class NetworkTranslator
	{
		public static string GetConfigPrefix(Network network)
		{
			Guard.NotNull(nameof(network), network);
			if (network == NBitcoin.Altcoins.Litecoin.Instance.Mainnet)
			{
				return "main";
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Testnet)
			{
				return "test";
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Regtest)
			{
				return "regtest";
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}

		public static IEnumerable<string> GetConfigPrefixesWithDots(Network network)
		{
			Guard.NotNull(nameof(network), network);

			yield return $"{GetConfigPrefix(network)}.";
			if (network == NBitcoin.Altcoins.Litecoin.Instance.Mainnet)
			{
				yield return "";
			}
		}

		public static string GetDataDirPrefix(Network network)
		{
			Guard.NotNull(nameof(network), network);
			if (network == NBitcoin.Altcoins.Litecoin.Instance.Mainnet)
			{
				return "";
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Testnet)
			{
				return "testnet3";
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Regtest)
			{
				return "regtest";
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}

		public static string GetCommandLineArguments(Network network)
		{
			Guard.NotNull(nameof(network), network);
			if (network == NBitcoin.Altcoins.Litecoin.Instance.Mainnet)
			{
				return "-regtest=0 -testnet=0";
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Testnet)
			{
				return "-regtest=0 -testnet=1";
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Regtest)
			{
				return "-regtest=1 -testnet=0";
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}
	}
}
