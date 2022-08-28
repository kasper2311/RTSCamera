﻿using RTSCamera.CommandSystem.Config;
using RTSCamera.CommandSystem.Logic.CombatAI;
using RTSCamera.CommandSystem.Utilities;
using TaleWorlds.MountAndBlade;

namespace RTSCamera.CommandSystem.Patch
{
    //[HarmonyLib.HarmonyPatch(typeof(MovementOrder), "Tick")]
    public class Patch_MovementOrder
    {
        public static bool GetSubstituteOrder_Prefix(MovementOrder __instance, ref MovementOrder __result,
            Formation formation)
        {
            if (__instance.OrderType == OrderType.ChargeWithTarget && CommandSystemConfig.Get().AttackSpecificFormation && CommandSystemConfig.Get().BehaviorAfterCharge == BehaviorAfterCharge.Hold && !formation.IsAIControlled)
            {
                var position = formation.QuerySystem.MedianPosition;
                position.SetVec2(formation.CurrentPosition);
                if (formation.Team == Mission.Current.PlayerTeam && formation.PlayerOwner == Agent.Main)
                {
                    Utility.DisplayFormationReadyMessage(formation);
                }
                __result = MovementOrder.MovementOrderMove(position);
                return false;
            }

            return true;
        }

        public static bool SetChargeBehaviorValues_Prefix(Agent unit)
        {
            if (Utility.ShouldChargeToFormation(unit))
            {
                UnitAIBehaviorValues.SetUnitAIBehaviorWhenChargeToFormation(unit);
                return false;
            }

            return true;
        }
    }
}
