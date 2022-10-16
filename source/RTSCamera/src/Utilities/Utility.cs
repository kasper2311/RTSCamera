﻿using RTSCamera.Config.HotKey;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace RTSCamera.Utilities
{
    public class Utility
    {
        public static void PrintUsageHint()
        {
            var keyName = RTSCameraGameKeyCategory.GetKey(GameKeyEnum.FreeCamera).ToSequenceString();
            var hint = Module.CurrentModule.GlobalTextManager.FindText("str_rts_camera_switch_camera_hint").SetTextVariable("KeyName", keyName).ToString();
            MissionSharedLibrary.Utilities.Utility.DisplayMessageForced(hint);
        }


        public static void UpdateMainAgentControllerInFreeCamera(Agent agent, Agent.ControllerType controller)
        {
            switch (controller)
            {
                case Agent.ControllerType.None:
                    MissionSharedLibrary.Utilities.Utility.PlayerControlAgent(agent);
                    agent.LookDirection = agent.GetMovementDirection().ToVec3();
                    break;
                case Agent.ControllerType.AI:
                    MissionSharedLibrary.Utilities.Utility.AIControlMainAgent(
                        Mission.Current.Mode != MissionMode.StartUp &&
                        Mission.Current.Mode != MissionMode.Conversation &&
                        Mission.Current.Mode != MissionMode.Stealth &&
                        Mission.Current.Mode != MissionMode.Barter &&
                        Mission.Current.Mode != MissionMode.Deployment &&
                        Mission.Current.Mode != MissionMode.Replay, true);
                    break;
                case Agent.ControllerType.Player:
                    MissionSharedLibrary.Utilities.Utility.PlayerControlAgent(agent);
                    break;
            }
        }

        public static void UpdateMainAgentControllerState(Agent agent, bool isSpectatorCamera, Agent.ControllerType playerControllerInFreeCamera)
        {

            var controller = Mission.Current.GetMissionBehavior<MissionMainAgentController>();
            if (controller != null)
            {
                if (agent.Controller == Agent.ControllerType.Player &&
                    (!isSpectatorCamera ||
                     playerControllerInFreeCamera == Agent.ControllerType.Player))
                {
                    controller.CustomLookDir = isSpectatorCamera ? agent.LookDirection : Vec3.Zero;
                    controller.Enable();
                }
                else
                {
                    controller.CustomLookDir = Vec3.Zero;
                    controller.Disable();
                    controller.InteractionComponent.ClearFocus();
                }
            }
        }

        public static bool IsArenaCombat(Mission mission)
        {
            foreach (var missionLogic in mission.MissionLogics)
            {
                if (missionLogic is ArenaAgentStateDeciderLogic)
                    return true;
            }

            return false;
        }

        public static bool IsBattleCombat(Mission mission)
        {
            return mission.Mode == MissionMode.Battle && mission.CombatType == Mission.MissionCombatType.Combat &&
                   !IsArenaCombat(mission);
        }
    }
}
