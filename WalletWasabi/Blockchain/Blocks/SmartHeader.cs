using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Blocks
{
	public class SmartHeader
	{
		public SmartHeader(uint256 blockHash, uint256 prevHash, uint height, DateTimeOffset blockTime)
		{
			BlockHash = Guard.NotNull(nameof(blockHash), blockHash);
			PrevHash = Guard.NotNull(nameof(prevHash), prevHash);
			if (blockHash == prevHash)
			{
				throw new InvalidOperationException($"{nameof(blockHash)} cannot be equal to {nameof(prevHash)}. Value: {blockHash}.");
			}

			Height = height;
			BlockTime = blockTime;
		}

		public uint256 BlockHash { get; }
		public uint256 PrevHash { get; }
		public uint Height { get; }
		public DateTimeOffset BlockTime { get; }

		#region SpecialHeaders

/* Starting header is bech32 release
   ---------------------------------
Litecoin core 0.16.0 release candidate adds bech32 support on 21-3-2018
- https://blog.litecoin.org/litecoin-core-v0-16-0-release-candidate-e1ac751d7f33
- https://blog.litecoin.org/litecoin-core-v0-16-0-release-5bf9b732b069
- https://www.reddit.com/r/litecoin/comments/85xquq/litecoin_core_v0160_release_candidate_litecoin/

=> LTC MAINNET block 1388888 7228180483d83c21f271874ec46a81aa0cf5ff36d07c9fb58e15ae8e5163bedc from 20-3-2018 at Mar 20, 2018 at 08:37 (epoch 1521578220) with parent block 1388887 at 84651d17c4869f8136b36c393cfe9d3bc7712c5921c87a2524dd48a2a895815b
*/
		private static SmartHeader StartingHeaderMain { get; } = new SmartHeader(
			new uint256("7228180483d83c21f271874ec46a81aa0cf5ff36d07c9fb58e15ae8e5163bedc"),
			new uint256("84651d17c4869f8136b36c393cfe9d3bc7712c5921c87a2524dd48a2a895815b"),
			1388888,
			DateTimeOffset.FromUnixTimeSeconds(1521578220));

		// Arbitrarily chosen Testnet Block
		private static SmartHeader StartingHeaderTestNet { get; } = new SmartHeader(
			new uint256("d9a4f13307e044144cf61c0950cad8af4911fbc7ec87e5081d5d5270b05637b6"),
			new uint256("fb0f72e00e8201d684d406376bf7684bb2107ae9f24f438c87d7782bd5563b25"),
			828575,
			DateTimeOffset.FromUnixTimeSeconds(1541027990));

		private static SmartHeader StartingHeaderRegTest { get; } = new SmartHeader(
			NBitcoin.Altcoins.Litecoin.Instance.Regtest.GenesisHash,
			NBitcoin.Altcoins.Litecoin.Instance.Regtest.GetGenesis().Header.HashPrevBlock,
			0,
			NBitcoin.Altcoins.Litecoin.Instance.Regtest.GetGenesis().Header.BlockTime);

		/// <summary>
		/// Where the first possible bech32 transaction ever can be found.
		/// </summary>
		public static SmartHeader GetStartingHeader(Network network)
			=> network.NetworkType switch
			{
				NetworkType.Mainnet => StartingHeaderMain,
				NetworkType.Testnet => StartingHeaderTestNet,
				NetworkType.Regtest => StartingHeaderRegTest,
				_ => throw new NotSupportedNetworkException(network)
			};

		#endregion SpecialHeaders
	}
}
