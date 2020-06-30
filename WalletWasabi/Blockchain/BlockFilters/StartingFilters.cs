using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Exceptions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.BlockFilters
{
	public static class StartingFilters
	{
		public static FilterModel GetStartingFilter(Network network)
		{
			var startingHeader = SmartHeader.GetStartingHeader(network);
			if (network == NBitcoin.Altcoins.Litecoin.Instance.Mainnet)
			{
				return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:{startingHeader.BlockHash}:{startingHeader.PrevHash}:{startingHeader.BlockTime.ToUnixTimeSeconds()}");
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Testnet)
			{
				return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:{startingHeader.BlockHash}:{startingHeader.PrevHash}:{startingHeader.BlockTime.ToUnixTimeSeconds()}");
			}
			else if (network == NBitcoin.Altcoins.Litecoin.Instance.Regtest)
			{
				GolombRiceFilter filter = IndexBuilderService.CreateDummyEmptyFilter(startingHeader.BlockHash);
				return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:{filter}:{startingHeader.PrevHash}:{startingHeader.BlockTime.ToUnixTimeSeconds()}");
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}
	}
}
