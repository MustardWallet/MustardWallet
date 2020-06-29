using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.Bitstamp;
using WalletWasabi.WebClients.CoinGecko;
using WalletWasabi.WebClients.Gemini;
using WalletWasabi.WebClients.ItBit;
using WalletWasabi.WebClients.SmartBit;
using System.Linq;

namespace WalletWasabi.WebClients
{
	public class ExchangeRateProvider : IExchangeRateProvider
	{
		private readonly IExchangeRateProvider[] ExchangeRateProviders =
		{
			// No LTC ? new BlockchainInfoExchangeRateProvider(),
			// TODO: check for LTC: new BitstampExchangeRateProvider(),
			// TODO: check for LTC: new CoinGeckoExchangeRateProvider(),
			new CoinbaseExchangeRateProvider(),
			new GeminiExchangeRateProvider()
			// No LTC? new ItBitExchangeRateProvider(),
			// No LTC? new SmartBitExchangeRateProvider(new SmartBitClient(Network.Main))
		};

		public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
		{
			foreach (var provider in ExchangeRateProviders)
			{
				try
				{
					return await provider.GetExchangeRateAsync();
				}
				catch (Exception ex)
				{
					// Ignore it and try with the next one
					Logger.LogTrace(ex);
				}
			}
			return Enumerable.Empty<ExchangeRate>();
		}
	}
}
