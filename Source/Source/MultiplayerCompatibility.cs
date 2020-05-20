using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using Multiplayer.API;
using HarmonyLib;

namespace Hospitality
{
    [StaticConstructorOnStartup]
    public static class MultiplayerCompatibility
    {
        static MethodInfo GetMapCompMethod;
        static MethodInfo GetCompMethod;

        internal static readonly HarmonyLib.Harmony hospitalityMultiplayerHarmony = new HarmonyLib.Harmony("hospitality.multiplayer.compat");

        static MultiplayerCompatibility()
        {
            if (!MP.enabled) return;

            Type type;

            GetMapCompMethod = AccessTools.Method(AccessTools.TypeByName("Hospitality.Hospitality_MapComponent"), "Instance");
            GetCompMethod = AccessTools.Method("Verse.ThingWithComps:GetComp").MakeGenericMethod(AccessTools.TypeByName("Hospitality.CompGuest"));

            MP.RegisterAll();

            ////bed gizmo
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Building_GuestBed:Swap"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Building_GuestBed:AdjustFee"));

            ////Guest MainTab
            //MP.RegisterSyncMethod(AccessTools.Method("Hospitality.MainTab.PawnColumnWorker_AccomodationArea:SetArea"));
            //MP.RegisterSyncMethod(AccessTools.Method("Hospitality.MainTab.PawnColumnWorker_ShoppingArea:SetArea"));
            //MP.RegisterSyncMethod(AccessTools.Method("Hospitality.MainTab.PawnColumnWorker_Entertain:SetValue"));
            //MP.RegisterSyncMethod(AccessTools.Method("Hospitality.MainTab.PawnColumnWorker_Recruit:SetValue"));

            ////Guest Tab
            //MP.RegisterSyncMethod(AccessTools.Method("Hospitality.GuestUtility:ForceRecruit"));
            //MP.RegisterSyncMethod(AccessTools.Method(AccessTools.TypeByName("Hospitality.CompGuest"),"SendHome"));

            //fields in Hospitality.CompGuest; chat, recruit, arrived, sentAway, guestArea_int, shoppingArea_int
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.CompGuest:SetEntertain"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.CompGuest:SetMakeFriends"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.CompGuest:SetArrived"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.CompGuest:SetSentAway"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.CompGuest:SetGuestArea"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.CompGuest:SetShoppingArea"));

            //fields in Hospitality.MapComponent; defaultInteractionMode, defaultAreaRestriction, defaultAreaShopping
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Hospitality_MapComponent:SetDefaultEntertain"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Hospitality_MapComponent:SetDefaultMakeFriends"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Hospitality_MapComponent:SetDefaultAreaRestriction"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Hospitality_MapComponent:SetDefaultAreaShopping"));
            MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Hospitality_MapComponent:SetRefuseGuestsUntilWeHaveBeds"));

            type = AccessTools.TypeByName("Hospitality.Hospitality_MapComponent");
            MP.RegisterSyncWorker<MapComponent>(SyncWorkerForMapComp, type);

            type = AccessTools.TypeByName("Hospitality.CompGuest");
            MP.RegisterSyncWorker<ThingComp>(SyncWorkerForCompGuest, type);

            hospitalityMultiplayerHarmony.Patch(AccessTools.Method("Hospitality.GenericUtility:DoAreaRestriction"),
                transpiler: new HarmonyMethod(typeof(MultiplayerCompatibility), nameof(StopRecursiveCall))
                );
        }

        static void SyncWorkerForMapComp(SyncWorker sync, ref MapComponent comp)
        {
            if (sync.isWriting)
            {
                sync.Write(comp.map);
            }
            else
            {
                Map map = sync.Read<Map>();

                comp = (MapComponent)GetMapCompMethod.Invoke(null, new object[] { map });
            }
        }

        static void SyncWorkerForCompGuest(SyncWorker sync, ref ThingComp comp)
        {
            if (sync.isWriting)
            {
                sync.Write(comp.parent);
            }
            else
            {
                ThingWithComps pawn = sync.Read<Pawn>();

                comp = (ThingComp)GetCompMethod.Invoke(pawn, null);
            }
        }

        static IEnumerable<CodeInstruction> StopRecursiveCall(IEnumerable<CodeInstruction> e, MethodBase original)
        {

            List<CodeInstruction> insts = new List<CodeInstruction>(e);
            CodeFinder finder = new CodeFinder(original, insts);

            var voidMethod = AccessTools.Method(typeof(MultiplayerCompatibility), nameof(Void));
            var offender = AccessTools.Property(AccessTools.TypeByName("RimWorld.Pawn_PlayerSettings"), "AreaRestriction").GetSetMethod();

            int position = new CodeFinder(original, insts).
                Forward(OpCodes.Callvirt, offender);

            insts.RemoveAt(position);
            insts.Insert(position, new CodeInstruction(OpCodes.Call, voidMethod));

            position = new CodeFinder(original, insts).
                Forward(OpCodes.Callvirt, offender).
                Forward(OpCodes.Callvirt, offender);

            insts.RemoveAt(position);
            insts.Insert(position, new CodeInstruction(OpCodes.Call, voidMethod));

            position = new CodeFinder(original, insts).
                Forward(OpCodes.Callvirt, offender).
                Forward(OpCodes.Callvirt, offender).
                Forward(OpCodes.Callvirt, offender);

            insts.RemoveAt(position);
            insts.Insert(position, new CodeInstruction(OpCodes.Call, voidMethod));

            return insts;
        }

        static void Void(object obj, Area area)
        {

        }

        private class CodeFinder
        {
            private MethodBase inMethod;
            private List<CodeInstruction> list;

            public int Pos { get; private set; }

            public CodeFinder(MethodBase inMethod, List<CodeInstruction> list)
            {
                this.inMethod = inMethod;
                this.list = list;
            }

            public CodeFinder Advance(int steps)
            {
                Pos += steps;
                return this;
            }

            public CodeFinder Forward(OpCode opcode, object operand = null)
            {
                Find(opcode, operand, 1);
                return this;
            }

            public CodeFinder Backward(OpCode opcode, object operand = null)
            {
                Find(opcode, operand, -1);
                return this;
            }

            public CodeFinder Find(OpCode opcode, object operand, int direction)
            {
                while (Pos < list.Count && Pos >= 0)
                {
                    if (Matches(list[Pos], opcode, operand)) return this;
                    Pos += direction;
                }

                throw new Exception($"Couldn't find instruction ({opcode}) with operand ({operand}) in {inMethod.FullDescription()}.");
            }

            public CodeFinder Find(Predicate<CodeInstruction> predicate, int direction)
            {
                while (Pos < list.Count && Pos >= 0)
                {
                    if (predicate(list[Pos])) return this;
                    Pos += direction;
                }

                throw new Exception($"Couldn't find instruction using predicate ({predicate.Method}) in method {inMethod.FullDescription()}.");
            }

            public CodeFinder Start()
            {
                Pos = 0;
                return this;
            }

            public CodeFinder End()
            {
                Pos = list.Count - 1;
                return this;
            }

            private bool Matches(CodeInstruction inst, OpCode opcode, object operand)
            {
                if (inst.opcode != opcode) return false;
                if (operand == null) return true;

                if (opcode == OpCodes.Stloc_S)
                    return (inst.operand as LocalBuilder).LocalIndex == (int)operand;

                return Equals(inst.operand, operand);
            }

            public static implicit operator int(CodeFinder finder)
            {
                return finder.Pos;
            }
        }
    }
}
