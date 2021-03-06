using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Hospitality
{
    public class LordJob_VisitColony : LordJob
    {
        private Faction faction;
        private IntVec3 chillSpot;
        private int stayDuration;
        private int checkEventId = -1;
        public bool getUpsetWhenLost;
        
        public LordJob_VisitColony()
        {
            // Required
        }

        public LordJob_VisitColony(Faction faction, IntVec3 chillSpot, int stayDuration, bool getUpsetWhenLost)
        {
            this.faction = faction;
            this.chillSpot = chillSpot;
            this.stayDuration = stayDuration;
            this.getUpsetWhenLost = getUpsetWhenLost;
        }

        public override bool NeverInRestraints => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref chillSpot, "chillSpot");
            Scribe_Values.Look(ref checkEventId, "checkEventId", -1);
            Scribe_Values.Look(ref stayDuration, "stayDuration", GenDate.TicksPerDay);
            Scribe_Values.Look(ref getUpsetWhenLost, "getUpsetWhenLost", true);
        }

        /*
        // Can't change the graph - not backwards compatible :(
        public override StateGraph CreateGraph()
        {
            StateGraph graphArrive = new StateGraph();
            var travelGraph = new LordJob_Travel(chillSpot).CreateGraph();
            travelGraph.StartingToil = new LordToil_CustomTravel(chillSpot, 0.49f, 85); // CHANGED: override StartingToil
            LordToil toilArrive = graphArrive.AttachSubgraph(travelGraph).StartingToil;
            var toilVisit = new LordToil_VisitPoint(); // CHANGED
            graphArrive.lordToils.Add(toilVisit);
            LordToil toilTakeWounded = new LordToil_TakeWoundedGuest();
            graphArrive.lordToils.Add(toilTakeWounded);
            StateGraph graphExit = new LordJob_TravelAndExit(IntVec3.Invalid).CreateGraph();
            LordToil toilExit = graphArrive.AttachSubgraph(graphExit).StartingToil;
            LordToil toilLeaveMap = graphExit.lordToils[1];
            LordToil toilLost = new LordToil_End();
            graphExit.AddToil(toilLost);
            Transition t1 = new Transition(toilArrive, toilVisit);
            t1.triggers.Add(new Trigger_Memo("TravelArrived"));
            graphArrive.transitions.Add(t1);
            LordToil_ExitMap toilExitCold = new LordToil_ExitMap(); // ADDED TOIL
            graphArrive.AddToil(toilExitCold);
            Transition t6 = new Transition(toilArrive, toilExitCold); // ADDED TRANSITION
            t6.triggers.Add(new Trigger_UrgentlyCold());
            t6.preActions.Add(new TransitionAction_Message("MessageVisitorsLeavingCold".Translate(faction.Name)));
            t6.preActions.Add(new TransitionAction_Custom(() => StopPawns(lord.ownedPawns)));
            graphArrive.transitions.Add(t6);
            Transition t2 = new Transition(toilVisit, toilTakeWounded);
            t2.triggers.Add(new Trigger_WoundedGuestPresent());
            //t2.preActions.Add(new TransitionAction_Message("MessageVisitorsTakingWounded".Translate(new object[] {faction.def.pawnsPlural.CapitalizeFirst(), faction.Name})));
            graphExit.transitions.Add(t2); // Moved to exit from arrive
            Transition t3 = new Transition(toilVisit, toilLeaveMap);
            t3.triggers.Add(new Trigger_BecamePlayerEnemy());
            t3.preActions.Add(new TransitionAction_WakeAll());
            t3.preActions.Add(new TransitionAction_SetDefendLocalGroup());
            graphArrive.transitions.Add(t3);
            Transition t4 = new Transition(toilArrive, toilExit);
            t4.triggers.Add(new Trigger_BecamePlayerEnemy());
            //t4.triggers.Add(new Trigger_VisitorsPleasedMax(MaxPleaseAmount(faction.ColonyGoodwill))); // CHANGED
            t4.triggers.Add(new Trigger_VisitorsAngeredMax(IncidentWorker_VisitorGroup.MaxAngerAmount(faction.PlayerGoodwill))); // CHANGED
            t4.preActions.Add(new TransitionAction_WakeAll());
            t4.preActions.Add(new TransitionAction_EnsureHaveExitDestination());
            graphArrive.transitions.Add(t4);
            Transition t5 = new Transition(toilVisit, toilExit);
            t5.triggers.Add(new Trigger_TicksPassedAndOkayToLeave(stayDuration));
            t5.triggers.Add(new Trigger_SentAway());
            t5.preActions.Add(new TransitionAction_Message("VisitorsLeaving".Translate(faction.Name)));
            t5.preActions.Add(new TransitionAction_WakeAll());
            t5.preActions.Add(new TransitionAction_EnsureHaveExitDestination());
            graphArrive.transitions.Add(t5);
            Transition t7 = new Transition(toilArrive, toilExitCold);
            t7.triggers.Add(new Trigger_SentAway());
            t7.preActions.Add(new TransitionAction_Message("VisitorsLeaving".Translate(faction.Name)));
            t7.preActions.Add(new TransitionAction_WakeAll());
            t7.preActions.Add(new TransitionAction_Custom(() => StopPawns(lord.ownedPawns)));
            t7.preActions.Add(new TransitionAction_Custom(() => LordToil_VisitPoint.DisplayLeaveMessage(Mathf.InverseLerp(-100, 100, faction.PlayerGoodwill), faction, lord.ownedPawns.Count, Map, true)));
            graphArrive.transitions.Add(t7);

            return graphArrive;
        }*/

        public override StateGraph CreateGraph()
        {
            StateGraph graphArrive = new StateGraph();
            StateGraph graphExit = new LordJob_TravelAndExit(IntVec3.Invalid).CreateGraph();
            StateGraph travelGraph = new LordJob_Travel(chillSpot).CreateGraph();
            travelGraph.StartingToil = new LordToil_CustomTravel(chillSpot, 0.49f, 85);
            // Arriving
            LordToil toilArriving = graphArrive.AttachSubgraph(travelGraph).StartingToil;
            // Visiting
            var toilVisiting = new LordToil_VisitPoint();
            graphArrive.lordToils.Add(toilVisiting);
            // Exit
            LordToil toilExit = graphArrive.AttachSubgraph(graphExit).StartingToil;
            // Leave map
            LordToil toilLeaveMap = graphExit.lordToils[1];
            // Take wounded
            LordToil toilTakeWounded = new LordToil_TakeWoundedGuest {lord = lord}; // This fixes the issue of missing lord when showing leave message
            graphExit.AddToil(toilTakeWounded);
            // Exit (TODO: Remove for 1.2)
            LordToil_ExitMap toilExitMap = new LordToil_ExitMap();
            graphArrive.AddToil(toilExitMap);
            // Arrived
            {
                Transition t1 = new Transition(toilArriving, toilVisiting);
                t1.triggers.Add(new Trigger_Memo("TravelArrived"));
                graphArrive.transitions.Add(t1);
            }
            // Too cold / hot
            {
                Transition t6 = new Transition(toilArriving, toilExit);
                t6.AddTrigger(new Trigger_PawnExperiencingDangerousTemperatures());
                t6.AddPreAction(new TransitionAction_Message("MessageVisitorsDangerousTemperature".Translate(faction?.def.pawnsPlural.CapitalizeFirst(), faction?.Name)));
                t6.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
                t6.AddPostAction(new TransitionAction_EndAllJobs());
                graphArrive.AddTransition(t6);
            }
            // Became enemy while arriving
            {
                Transition t3 = new Transition(toilVisiting, toilLeaveMap);
                t3.triggers.Add(new Trigger_BecamePlayerEnemy());
                t3.preActions.Add(new TransitionAction_SetDefendLocalGroup());
                t3.postActions.Add(new TransitionAction_WakeAll());
                graphArrive.transitions.Add(t3);
            }
            // Leave if became angry
            {
                Transition t4 = new Transition(toilArriving, toilExit);
                t4.triggers.Add(new Trigger_BecamePlayerEnemy());
                t4.triggers.Add(new Trigger_VisitorsAngeredMax(IncidentWorker_VisitorGroup.MaxAngerAmount(faction?.PlayerGoodwill ?? 0)));
                t4.preActions.Add(new TransitionAction_EnsureHaveExitDestination());
                t4.postActions.Add(new TransitionAction_WakeAll());
                graphArrive.transitions.Add(t4);
            }
            // Leave if stayed long enough
            {
                Transition t5 = new Transition(toilVisiting, toilExit);
                t5.triggers.Add(new Trigger_TicksPassedAndOkayToLeave(stayDuration));
                t5.triggers.Add(new Trigger_SentAway()); // Sent away during stay
                t5.preActions.Add(new TransitionAction_Message("VisitorsLeaving".Translate(faction?.Name)));
                t5.preActions.Add(new TransitionAction_EnsureHaveExitDestination());
                t5.postActions.Add(new TransitionAction_WakeAll());
                graphArrive.transitions.Add(t5);
            }
            // Leave if sent away (before fully arriving!)
            {
                Transition t7 = new Transition(toilArriving, toilExit);
                t7.triggers.Add(new Trigger_SentAway());
                t7.preActions.Add(new TransitionAction_Message("VisitorsLeaving".Translate(faction?.Name)));
                t7.preActions.Add(new TransitionAction_WakeAll());
                t7.preActions.Add(new TransitionAction_SendAway());
                graphArrive.transitions.Add(t7);
            }
            // Take wounded guest when arriving
            {
                Transition t8 = new Transition(toilArriving, toilTakeWounded);
                t8.AddTrigger(new Trigger_WoundedGuestPresent());
                t8.AddPreAction(new TransitionAction_Message("MessageVisitorsTakingWounded".Translate(faction?.def.pawnsPlural.CapitalizeFirst(), faction?.Name)));
                graphArrive.AddTransition(t8);
            }
            // Take wounded guest when leaving - couldn't get this to work
            //{
            //Transition t9 = new Transition(toilExit, toilTakeWounded);
            //t9.AddTrigger(new Trigger_WoundedGuestPresent());
            //t9.AddPreAction(new TransitionAction_Message("MessageVisitorsTakingWounded".Translate(faction.def.pawnsPlural.CapitalizeFirst(), faction.Name)));
            //graphExit.AddTransition(t9);
            //}

            return graphArrive;
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            if (condition == PawnLostCondition.ExitedMap) return;

            pawn.ownership.UnclaimAll();

            if (!lord.ownedPawns.Any())
            {
                GuestUtility.OnLostEntireGroup(lord);
            }
        }
    }
}