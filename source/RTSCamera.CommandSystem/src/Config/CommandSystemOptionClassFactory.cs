﻿using MissionLibrary.Provider;
using MissionLibrary.View;
using MissionSharedLibrary.Provider;
using MissionSharedLibrary.View.ViewModelCollection;
using MissionSharedLibrary.View.ViewModelCollection.Options;
using RTSCamera.CommandSystem.Patch;
using RTSCamera.CommandSystem.Patch.CircularFormation;
using RTSCamera.View;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using FormationColorMissionView = RTSCamera.CommandSystem.View.FormationColorMissionView;

namespace RTSCamera.CommandSystem.Config
{
    public class CommandSystemOptionClassFactory
    {
        public static IIdProvider<AOptionClass> CreateOptionClassProvider(IMenuClassCollection menuClassCollection)
        {
            return IdProviderCreator.Create(() =>
            {
                var contourView = Mission.Current.GetMissionBehaviour<FormationColorMissionView>();

                var optionClass = new OptionClass(CommandSystemSubModule.ModuleId,
                    GameTexts.FindText("str_rts_camera_command_system_option_class"), menuClassCollection);
                var commandOptionCategory = new OptionCategory("Command", GameTexts.FindText("str_rts_camera_command_system_command_system_options"));
                commandOptionCategory.AddOption(new BoolOptionViewModel(
                    GameTexts.FindText("str_rts_camera_command_system_click_to_select_formation"),
                    GameTexts.FindText("str_rts_camera_command_system_click_to_select_formation_hint"),
                    () => CommandSystemConfig.Get().ClickToSelectFormation, b =>
                    {
                        CommandSystemConfig.Get().ClickToSelectFormation = b;
                        contourView?.SetEnableContourForSelectedFormation(b);
                    }));
                commandOptionCategory.AddOption(new BoolOptionViewModel(
                    GameTexts.FindText("str_rts_camera_command_system_attack_specific_formation"),
                    GameTexts.FindText("str_rts_camera_command_system_attack_specific_formation_hint"),
                    () => CommandSystemConfig.Get().ClickToSelectFormation, b =>
                    {
                        CommandSystemConfig.Get().ClickToSelectFormation = b;
                        if (b)
                            PatchChargeToFormation.Patch();
                        else
                            PatchChargeToFormation.UnPatch();
                    }));
                commandOptionCategory.AddOption(new BoolOptionViewModel(
                    GameTexts.FindText("str_rts_camera_command_system_fix_circular_arrangement"), null,
                    () => CommandSystemConfig.Get().FixCircularArrangement,
                    b =>
                    {
                        CommandSystemConfig.Get().FixCircularArrangement = b;
                        if (b)
                            PatchCircularFormation.Patch();
                        else
                            PatchCircularFormation.UnPatch();
                    }));
                optionClass.AddOptionCategory(0, commandOptionCategory);

                return optionClass;
            }, CommandSystemSubModule.ModuleId);
        }
    }
}