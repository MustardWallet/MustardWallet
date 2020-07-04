[Mustard Wallet for Litecoin](https://MustardWallet.com) is an open-source, non-custodial, privacy-focused Litecoin wallet for desktop, that implements [Chaumian CoinJoin](https://github.com/nopara73/ZeroLink/#ii-chaumian-coinjoin). Based on Wasabi Wallet for Bitcoin .

The main privacy features on the network level:
- Tor-only by default.
- BIP 158 block filters for private light client.
- Opt-in connection to user full node.

and on the blockchain level:
- Intuitive ZeroLink CoinJoin integration.
- Superb coin selection and labeling.
- Dust attack protections.

For more information, please check out the [Wasabi Documentation](https://docs.wasabiwallet.io), an archive of knowledge about the nuances of Bitcoin privacy and how to properly use Wasabi.

Mustard Wallet specific documentation can be found on [Mustard Wallet's GitHub Page](https://github.com/MustardWallet/) .

# [Download Mustard Wallet for Litecoin](https://github.com/MustardWallet/MustardWalletLTC/releases)

![](https://i.imgur.com/Y9fwGmQ.png)

For step by step instructions package installation instructions, see the [documentation](https://docs.wasabiwallet.io/using-wasabi/InstallPackage.html)

# Build From Source Code

## Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core 3.1 SDK: https://www.microsoft.com/net/download
3. Optionally disable .NET's telemetry by typing `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and macOS or `setx DOTNET_CLI_TELEMETRY_OPTOUT 1` on Windows.

## Get Mustard

Clone & Restore & Build

```sh
git clone https://github.com/MustardWallet/MustardWalletLTC.git
cd MustardWalletLTC/WalletWasabi.Gui
dotnet build
```

## Run Mustard Wallet

Run Mustard Wallet with `dotnet run` from the `WalletWasabi.Gui` folder.

## Update Mustard Wallet

```sh
git pull
```
