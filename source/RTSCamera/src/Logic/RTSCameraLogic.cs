﻿using RTSCamera.Logic.SubLogic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RTSCamera.Logic
{
    public class RTSCameraLogic : MissionLogic
    {
        public CommanderLogic CommanderLogic;
        public DisableDeathLogic DisableDeathLogic;
        public FixScoreBoardAfterPlayerDeadLogic FixScoreBoardAfterPlayerDeadLogic;
        public MissionSpeedLogic MissionSpeedLogic;
        public SwitchFreeCameraLogic SwitchFreeCameraLogic;
        public SwitchTeamLogic SwitchTeamLogic;
        public ControlTroopLogic ControlTroopLogic;
        public CampaignSkillLogic CampaignSkillLogic;
        public static RTSCameraLogic Instance;

        public RTSCameraLogic()
        {
            CommanderLogic = new CommanderLogic(this);
            DisableDeathLogic = new DisableDeathLogic(this);
            FixScoreBoardAfterPlayerDeadLogic = new FixScoreBoardAfterPlayerDeadLogic(this);
            MissionSpeedLogic = new MissionSpeedLogic(this);
            SwitchFreeCameraLogic = new SwitchFreeCameraLogic(this);
            SwitchTeamLogic = new SwitchTeamLogic(this);
            ControlTroopLogic = new ControlTroopLogic(this);
            CampaignSkillLogic = new CampaignSkillLogic(this);
        }

        public override void OnCreated()
        {
            base.OnCreated();

            Instance = this;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();

            CommanderLogic.OnBehaviourInitialize();
            FixScoreBoardAfterPlayerDeadLogic.OnBehaviourInitialize();
            SwitchFreeCameraLogic.OnBehaviourInitialize();
            SwitchTeamLogic.OnBehaviourInitialize();
            ControlTroopLogic.OnBehaviourInitialize();
            CampaignSkillLogic.OnBehaviourInitialize();
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();

            CommanderLogic.OnRemoveBehaviour();
            FixScoreBoardAfterPlayerDeadLogic.OnRemoveBehaviour();
            SwitchFreeCameraLogic.OnRemoveBehaviour();
            
            Instance = null;
        }

        public override void AfterStart()
        {
            base.AfterStart();

            DisableDeathLogic.AfterStart();
            MissionSpeedLogic.AfterStart();
        }

        public override void AfterAddTeam(Team team)
        {
            base.AfterAddTeam(team);

            SwitchFreeCameraLogic.AfterAddTeam(team);
            CampaignSkillLogic.AfterAddTeam(team);
        }

        public override void OnFormationUnitsSpawned(Team team)
        {
            base.OnFormationUnitsSpawned(team);

            SwitchFreeCameraLogic.OnFormationUnitsSpawned(team);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            DisableDeathLogic.OnMissionTick(dt);
            MissionSpeedLogic.OnMissionTick(dt);
            SwitchFreeCameraLogic.OnMissionTick(dt);
            SwitchTeamLogic.OnMissionTick(dt);
            ControlTroopLogic.OnMissionTick(dt);
        }

        public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
        {
            base.OnMissionModeChange(oldMissionMode, atStart);

            SwitchFreeCameraLogic.OnMissionModeChange(oldMissionMode, atStart);
            CampaignSkillLogic.OnMissionModeChange(oldMissionMode, atStart);
        }

        protected override void OnAgentControllerChanged(Agent agent, Agent.ControllerType oldController)
        {
            base.OnAgentControllerChanged(agent, oldController);

            SwitchFreeCameraLogic.OnAgentControllerChanged(agent);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);

            SwitchFreeCameraLogic.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        }
    }
}
