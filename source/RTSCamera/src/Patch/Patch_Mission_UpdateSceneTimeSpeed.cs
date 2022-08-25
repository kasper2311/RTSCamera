﻿using HarmonyLib;
using MissionSharedLibrary.Utilities;
using RTSCamera.Config;
using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace RTSCamera.Patch
{
    public class Patch_Mission_UpdateSceneTimeSpeed
    {
        private static readonly Harmony Harmony = new Harmony(RTSCameraSubModule.ModuleId + "_" + nameof(Patch_Mission_UpdateSceneTimeSpeed));

        private static bool _patched;
        public static bool Patch()
        {
            try
            {
                if (_patched)
                    return false;
                _patched = true;

                Harmony.Patch(
                    typeof(Mission).GetMethod("UpdateSceneTimeSpeed",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    postfix: new HarmonyMethod(typeof(Patch_Mission_UpdateSceneTimeSpeed).GetMethod(
                        nameof(Postfix_UpdateSceneTimeSpeed), BindingFlags.Static | BindingFlags.Public)));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Utility.DisplayMessage(e.ToString());
                return false;
            }

            return true;
        }
        public static void Postfix_UpdateSceneTimeSpeed(Mission __instance)
        {
            if (RTSCameraConfig.Get().SlowMotionMode)
                __instance.Scene.TimeSpeed = RTSCameraConfig.Get().SlowMotionFactor;
        }
    }
}
