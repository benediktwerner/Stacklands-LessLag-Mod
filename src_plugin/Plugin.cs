using System.Collections.Generic;
using System.Reflection.Emit;
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

        static ConfigEntry<bool> FastCardPush;
        static ConfigEntry<int> PushEveryFrames;
        static ConfigEntry<int> BuyBoosterBoxEveryFrames;

        static int UpdatePushFrame = 1;
        static int FrameCount = 0;

        private void Awake()
        {
            logger = Logger;
            FastCardPush = Config.Bind("General", "Fast Card Push", true);
            PushEveryFrames = Config.Bind(
                "General",
                "Check Card Push Every X Frames",
                5,
                "Values above 1 make card pushing more janky but give large performance benefits"
            );
            BuyBoosterBoxEveryFrames = Config.Bind(
                "General",
                "Update BuyBoosterBox Every X Frames",
                1,
                "Values above 1 make the booster shop a bit janky for a minor performance benefit"
            );

            PushEveryFrames.SettingChanged += (_, _) => UpdatePushFrame = FrameCount + 1;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        public void Update()
        {
            FrameCount++;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuyBoosterBox), nameof(BuyBoosterBox.Update))]
        public static void ReduceBuyBoosterBoxUpdate(
            BuyBoosterBox __instance,
            out bool __runOriginal
        )
        {
            __runOriginal = FrameCount % BuyBoosterBoxEveryFrames.Value == 0;
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(CardData), nameof(CardData.UpdateCard))]
        public static void CardData_UpdateCard(CardData __instance) { }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Equipable), nameof(Equipable.UpdateCard))]
        public static void ReduceEquipableUpdate(Equipable __instance, out bool __runOriginal)
        {
            __runOriginal = !__instance.BeneUpdatedOnce;
            if (!__runOriginal)
                CardData_UpdateCard(__instance);
            else
                __instance.BeneUpdatedOnce = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameCard), nameof(GameCard.LateUpdate))]
        public static void GameCardPostUpdate(GameCard __instance, out bool __runOriginal)
        {
            if (__runOriginal = !FastCardPush.Value)
                return;

            if (__instance.MyBoard == null || !__instance.MyBoard.IsCurrent)
                return;

            if (__instance.Parent == null && __instance.EquipmentHolder == null)
                __instance.ClampPos();

            if (__instance.Parent != null)
                __instance.LastParent = __instance.Parent;
            else
            {
                if (FrameCount == UpdatePushFrame)
                    __instance.BeneFrameMod = Random.RandomRangeInt(0, PushEveryFrames.Value);
                if (FrameCount % PushEveryFrames.Value == __instance.BeneFrameMod)
                    PushAwayFromOthers(__instance);
            }

            if (__instance.removedChild == null || !__instance.removedChild.BeingDragged)
                __instance.BeneLastChild = __instance.Child;
        }

        static void PushAwayFromOthers(GameCard root)
        {
            if (!root.CanBePushed())
                return;

            var child = root;
            do
            {
                child.BeneRoot = root;
                child = child.Child;
            } while (child != null);

            child = root;
            while (true)
            {
                int num = PhysicsExtensions.OverlapBoxNonAlloc(
                    child.boxCollider,
                    child.hits,
                    -5,
                    0
                );
                for (int i = 0; i < num; i++)
                {
                    var component = child.hits[i].gameObject.GetComponent<Draggable>();
                    if (
                        component != null
                        && !(component is GameCard g && g.BeneRoot == root)
                        && !component.BeingDragged
                        && CanBePushedBy(child, component)
                    )
                    {
                        Vector3 vector = component.transform.position - root.transform.position;
                        vector.y = 0f;
                        float num2 = root.Mass + component.Mass;
                        float num3 = 1f - root.Mass / num2;
                        if (component.PushDir != null)
                        {
                            vector = component.PushDir.Value;
                            num3 = 1f;
                        }
                        root.TargetPosition -=
                            num3 * vector.normalized * 2f * Time.deltaTime * PushEveryFrames.Value;
                        return;
                    }
                }

                if (child.Child == null)
                    return;
                child = child.Child;

                for (int i = 0; i < 5; i++)
                {
                    if (child.Child == null)
                        break;
                    child = child.Child;
                }
            }
        }

        static bool CanBePushedBy(GameCard card, Draggable other)
        {
            if (other is GameCard otherCard)
            {
                if (
                    otherCard.BounceTarget != null
                    || otherCard.Destroyed
                    || !otherCard.PushEnabled
                    || (otherCard.CardData is Food && WorldManager.instance.InEatingAnimation)
                    || otherCard.IsEquipped
                    || !card.CardData.CanBePushedBy(otherCard.CardData)
                )
                    return false;
            }
            return (other is not InventoryInteractable)
                && !other.BeingDragged
                && (other.Velocity == null || other.Velocity.Value.y < 0f);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(CardData), nameof(CardData.UpdateCard))]
        public static IEnumerable<CodeInstruction> UpdateCardTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            // if (this.MyGameCard.Parent == null)
            // -> add && StackChanged(this.MyGameCard)
            // and remove the second bluepring computation after this if-else
            var matcher = new CodeMatcher(instructions)
                .MatchForward(
                    true,
                    new CodeMatch(OpCodes.Br),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldstr, "FinishBlueprint"),
                    new CodeMatch(OpCodes.Call),
                    new CodeMatch(
                        OpCodes.Callvirt,
                        AccessTools.Method(typeof(GameCard), nameof(GameCard.CancelTimer))
                    ),
                    new CodeMatch(OpCodes.Br)
                )
                .ThrowIfInvalid("Didn't find CancelTimer(FinishBlueprint)");
            var label = matcher.Instruction.operand;
            return matcher
                .MatchForward(true, new CodeMatch(OpCodes.Callvirt))
                .ThrowIfInvalid("Didn't find second CancelTimer(FinishBlueprint)")
                .Advance(1)
                .SetOpcodeAndAdvance(OpCodes.Nop)
                .RemoveInstructions(18)
                .MatchBack(
                    true,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(
                        OpCodes.Ldfld,
                        AccessTools.Field(typeof(CardData), nameof(CardData.MyGameCard))
                    ),
                    new CodeMatch(
                        OpCodes.Ldfld,
                        AccessTools.Field(typeof(GameCard), nameof(GameCard.Parent))
                    ),
                    new CodeMatch(OpCodes.Ldnull),
                    new CodeMatch(OpCodes.Call),
                    new CodeMatch(OpCodes.Brfalse)
                )
                .ThrowIfInvalid("Didn't find Parent == null check")
                .Advance(1)
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(
                        OpCodes.Ldfld,
                        AccessTools.Field(typeof(CardData), nameof(CardData.MyGameCard))
                    ),
                    new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(Plugin), nameof(Plugin.StackChanged))
                    ),
                    new CodeInstruction(OpCodes.Brfalse, label)
                )
                .InstructionEnumeration();
        }

        static bool StackChanged(GameCard card)
        {
            while (card != null)
            {
                if (card.BeneLastChild != card.Child)
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
