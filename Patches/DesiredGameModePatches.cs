using GorillaGameModes;
using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Utilla.Models;
using Utilla.Utils;

namespace Utilla.Patches
{
    [HarmonyPatch(typeof(GorillaNetworkJoinTrigger))]
    internal class DesiredGameModePatches
    {
        // Cache: (instance, gameMode) -> result string, avoids recomputing every frame
        private static readonly Dictionary<(GorillaNetworkJoinTrigger, string), (bool runOriginal, string result)> _cache
            = new Dictionary<(GorillaNetworkJoinTrigger, string), (bool, string)>();

        [HarmonyPatch(nameof(GorillaNetworkJoinTrigger.GetDesiredGameType)), HarmonyPrefix]
        public static bool DesiredGameTypePatch(GorillaNetworkJoinTrigger __instance, ref string __result, ref GTZone ___zone)
        {
            Type joinTriggerType = __instance.GetType();

            if (joinTriggerType == typeof(GorillaNetworkRankedJoinTrigger) || ___zone == GTZone.ranked)
            {
                __result = GameModeType.InfectionCompetitive.ToString();
                return false;
            }

            string currentGameMode = GorillaComputer.instance.currentGameMode.Value;
            var cacheKey = (__instance, currentGameMode);

            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                __result = cached.result;
                return cached.runOriginal;
            }

            bool runOriginal = true;
            string resultValue = null;

            if (!Enum.IsDefined(typeof(GameModeType), currentGameMode))
            {
                if (GameModeUtils.GetGamemodeFromId(currentGameMode) is Gamemode gamemode && gamemode.BaseGamemode.HasValue && gamemode.BaseGamemode.Value < GameModeType.Count)
                {
                    GameModeType gameModeType = gamemode.BaseGamemode.Value;
                    GameModeType verifiedGameMode = GameMode.GameModeZoneMapping.VerifyModeForZone(__instance.zone, gameModeType, NetworkSystem.Instance.SessionIsPrivate);

                    resultValue = verifiedGameMode == gameModeType ? currentGameMode : verifiedGameMode.ToString();
                    runOriginal = false;
                }
                else
                {
                    resultValue = currentGameMode;
                    runOriginal = false;
                }
            }

            _cache[cacheKey] = (runOriginal, resultValue);
            __result = resultValue;
            return runOriginal;
        }

        [HarmonyPatch(nameof(GorillaNetworkJoinTrigger.GetDesiredGameTypeLocalized)), HarmonyPrefix]
        public static bool DesiredLocalizedGameTypePatch(GorillaNetworkJoinTrigger __instance, ref string __result, ref GTZone ___zone)
            => DesiredGameTypePatch(__instance, ref __result, ref ___zone);

        // Clear cache when game mode changes so stale results aren't returned
        public static void ClearCache() => _cache.Clear();
    }
}
