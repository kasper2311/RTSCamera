﻿using MissionLibrary.Controller;
using MissionSharedLibrary.Controller;
using RTSCamera.CommandSystem.Config;
using RTSCamera.CommandSystem.Logic;
using RTSCamera.CommandSystem.Patch;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;

namespace RTSCamera.CommandSystem
{
    public class CommandSystemMissionStartingHandler : AMissionStartingHandler

    {
        public override void OnCreated(MissionView entranceView)
        {
            List<MissionBehavior> list = new List<MissionBehavior>
            {
                new CommandSystemLogic(),
            };

            foreach (var MissionBehavior in list)
            {
                MissionStartingManager.AddMissionBehavior(entranceView, MissionBehavior);
            }
        }

        public override void OnPreMissionTick(MissionView entranceView, float dt)
        {
            var config = CommandSystemConfig.Get();
            if (config.AttackSpecificFormation)
            {
                PatchChargeToFormation.Patch();
            }
        }
    }
}
