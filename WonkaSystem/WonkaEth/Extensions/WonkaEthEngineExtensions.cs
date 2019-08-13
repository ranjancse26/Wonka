﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3.Accounts;

using WonkaBre;
using WonkaBre.RuleTree;
using WonkaEth.Contracts;
using WonkaEth.Init;
using WonkaRef;

namespace WonkaEth.Extensions
{
    public static class WonkaEthEngineExtensions
    {
        private const int CONST_DEPLOY_ENGINE_CONTRACT_GAS_COST  = 8388608;
		private const int CONST_DEPLOY_DEFAULT_CONTRACT_GAS_COST = 1000000;

		/// <summary>
		/// 
		/// This method will initialize an instance of the Wonka.Net engine, using all the data provided.
		/// 
		/// <returns>None</returns>
		/// </summary>
		public static void InitEngine(this WonkaEthEngineInitialization poEngineInitData, bool pbRequireRetrieveValueMethod = true)
		{
			var EngineProps = poEngineInitData.Engine;

			if (EngineProps == null)
				throw new Exception("ERROR!  No engine properties provided.");

			if ((EngineProps.RulesEngine == null) && !String.IsNullOrEmpty(EngineProps.RulesMarkupXml))
			{
				if (pbRequireRetrieveValueMethod && (EngineProps.DotNetRetrieveMethod == null))
					throw new WonkaEthInitException("ERROR!  Retrieve method not provided for the Wonka.NET engine.", poEngineInitData);

				if (EngineProps.MetadataSource == null)
					throw new WonkaEthInitException("ERROR!  No metadata source has been provided.", poEngineInitData);

				// Using the metadata source, we create an instance of a defined data domain
				WonkaRefEnvironment WonkaRefEnv = WonkaRefEnvironment.CreateInstance(false, EngineProps.MetadataSource);

				var account = new Account(poEngineInitData.EthPassword);

				Nethereum.Web3.Web3 web3 = null;
				if (!String.IsNullOrEmpty(poEngineInitData.Web3HttpUrl))
					web3 = new Nethereum.Web3.Web3(account, poEngineInitData.Web3HttpUrl);
				else
					web3 = new Nethereum.Web3.Web3(account);

				if (String.IsNullOrEmpty(poEngineInitData.RulesEngineContractAddress))
				{
					var EngineDeployment = new Autogen.WonkaEngine.WonkaEngineDeployment();

					HexBigInteger nDeployGas = new HexBigInteger(CONST_DEPLOY_ENGINE_CONTRACT_GAS_COST);

					poEngineInitData.RulesEngineContractAddress =
						EngineDeployment.DeployContract(web3, poEngineInitData.RulesEngineABI, poEngineInitData.EthSenderAddress, nDeployGas, poEngineInitData.Web3HttpUrl);
				}

				if (String.IsNullOrEmpty(poEngineInitData.RegistryContractAddress))
				{
					var RegistryDeployment = new Autogen.WonkaRegistry.WonkaRegistryDeployment();

					HexBigInteger nDeployGas = new HexBigInteger(CONST_DEPLOY_DEFAULT_CONTRACT_GAS_COST);

					poEngineInitData.RegistryContractAddress =
						RegistryDeployment.DeployContract(web3, poEngineInitData.RegistryContractABI, poEngineInitData.EthSenderAddress, nDeployGas, poEngineInitData.Web3HttpUrl);
				}

				if (String.IsNullOrEmpty(poEngineInitData.StorageContractAddress))
				{
					var TestContractDeployment = new Autogen.WonkaTestContract.WonkaTestContractDeployment();

					HexBigInteger nDeployGas = new HexBigInteger(CONST_DEPLOY_DEFAULT_CONTRACT_GAS_COST);

					poEngineInitData.StorageContractAddress =
						TestContractDeployment.DeployContract(web3, poEngineInitData.StorageContractABI, poEngineInitData.EthSenderAddress, nDeployGas, poEngineInitData.Web3HttpUrl);
				}

				if ((EngineProps.SourceMap == null) || (EngineProps.SourceMap.Count == 0))
				{
					EngineProps.SourceMap = new Dictionary<string, WonkaBre.RuleTree.WonkaBreSource>();

					// Here a mapping is created, where each Attribute points to a specific contract and its "accessor" methods
					// - the class that contains this information (contract, accessors, etc.) is of the WonkaBreSource type
					foreach (WonkaRefAttr TempAttr in WonkaRefEnv.AttrCache)
					{
						WonkaBreSource TempSource =
							new WonkaBreSource(poEngineInitData.StorageDefaultSourceId,
											   poEngineInitData.EthSenderAddress, 
											   poEngineInitData.EthPassword, 
											   poEngineInitData.StorageContractAddress, 
											   poEngineInitData.StorageContractABI,
											   poEngineInitData.StorageGetterMethod, 
											   poEngineInitData.StorageSetterMethod,
											   EngineProps.DotNetRetrieveMethod);

						EngineProps.SourceMap[TempAttr.AttrName] = TempSource;
					}
				}

				EngineProps.RulesEngine = 
					new WonkaBreRulesEngine(new StringBuilder(EngineProps.RulesMarkupXml), EngineProps.SourceMap, EngineProps.MetadataSource);

				EngineProps.RulesEngine.DefaultSource = poEngineInitData.StorageDefaultSourceId;

				EngineProps.RulesEngine.SetDefaultStdOps(poEngineInitData.EthPassword, poEngineInitData.Web3HttpUrl);
			}
		}

		/// <summary>
		/// 
		/// This method will use Nethereum to call upon (or create) an instance of the Ethgine contract and 
		/// to create a RuleTree that will be owned by the Sender.
		/// 
		/// <returns>Indicates whether or not the RuleTree was created to the blockchain</returns>
		/// </summary>
		public static bool Serialize(this WonkaEthEngineInitialization poEngineInitData)
        {
            bool bResult = false;

            if ((poEngineInitData != null) && (poEngineInitData.Engine != null) && (poEngineInitData.Engine.RulesEngine != null))
            {
                if (poEngineInitData.Engine.RulesEngine.RefEnvHandle != null)
                {
                    bResult =
                        poEngineInitData.Engine.RulesEngine.RefEnvHandle.Serialize(poEngineInitData.EthRuleTreeOwnerAddress,
                                                                                   poEngineInitData.EthPassword,
                                                                                   poEngineInitData.EthSenderAddress,
                                                                                   poEngineInitData.RulesEngineContractAddress,
                                                                                   poEngineInitData.RulesEngineABI,
                                                                                   poEngineInitData.Web3HttpUrl);

                    if (bResult)
                    {
                        bResult =
                            poEngineInitData.Engine.RulesEngine.Serialize(poEngineInitData.EthRuleTreeOwnerAddress,
                                                                          poEngineInitData.EthPassword,
                                                                          poEngineInitData.EthSenderAddress,
                                                                          poEngineInitData.RulesEngineContractAddress,
                                                                          poEngineInitData.RulesEngineABI,
                                                                          poEngineInitData.RulesEngineContractAddress,
                                                                          poEngineInitData.Web3HttpUrl);
                    }
                }
            }

            return bResult;
        }
    }
}
