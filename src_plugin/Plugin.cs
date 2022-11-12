using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace LessLag
{
    [BepInPlugin(
        "de.benediktwerner.stacklands.lesslag",
        PluginInfo.PLUGIN_NAME,
        PluginInfo.PLUGIN_VERSION
    )]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static ConfigEntry<bool> optFindMatchingPrint;

        private void Awake()
        {
            logger = Logger;
            optFindMatchingPrint = Config.Bind("General", "OptimizeFindMatchingPrint", true);
            Harmony.CreateAndPatchAll(typeof(Plugin));
            logger.LogDebug("bene:");
            logger.LogDebug(AccessTools.Field(typeof(CardData), "Bene"));
        }

        public static int FrameCount = 0;

        static float nextClear = 0;
        static Dictionary<CardData, Subprint> previousSubprint =
            new Dictionary<CardData, Subprint>();

        // static HashSet<CardData> descriptionSet;

        void Update()
        {
            FrameCount++;

            if (Time.time > nextClear)
            {
                nextClear = Time.time + 30 + 30 * Random.value;
                var newDict = new Dictionary<CardData, Subprint>();
                foreach (var item in previousSubprint)
                {
                    if (!item.Key.MyGameCard.Destroyed)
                        newDict[item.Key] = item.Value;
                }
                previousSubprint = newDict;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CardData), nameof(CardData.FindMatchingPrint))]
        public static void OptimizeFindMatchingPrint(
            CardData __instance,
            out Subprint __result,
            out bool __runOriginal
        )
        {
            __result = null;
            if (__runOriginal = !optFindMatchingPrint.Value)
                return;

            if (
                !StackChanged(__instance.MyGameCard)
                && previousSubprint.TryGetValue(__instance, out var print)
            )
            {
                __result = print;
                return;
            }

            int fullyMatchedAt = int.MaxValue;
            int matchCount = int.MinValue;
            foreach (var blueprint in WorldManager.instance.BlueprintPrefabs)
            {
                Subprint matchingSubprint = blueprint.GetMatchingSubprint(
                    __instance.MyGameCard,
                    out var subprintMatchInfo
                );
                if (
                    matchingSubprint != null
                    && (
                        subprintMatchInfo.MatchCount > matchCount
                        || (
                            subprintMatchInfo.MatchCount == matchCount
                            && subprintMatchInfo.FullyMatchedAt < fullyMatchedAt
                        )
                    )
                )
                {
                    fullyMatchedAt = subprintMatchInfo.FullyMatchedAt;
                    matchCount = subprintMatchInfo.MatchCount;
                    __result = matchingSubprint;
                }
            }

            previousSubprint[__instance] = __result;
        }

        static bool StackChanged(GameCard card)
        {
            while (card != null)
            {
                if (card.LastParent != card.Parent)
                    return true;
                card = card.Child;
            }
            return false;
        }
    }
}


// spritemanager update
// gamescreen update
// worldmanager update
// gameboard update
// inventoryinteractable
// equipped equipment
