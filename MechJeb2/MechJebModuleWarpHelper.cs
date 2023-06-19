﻿using System;
using System.Linq;
using JetBrains.Annotations;
using KSP.Localization;
using UnityEngine;

namespace MuMech
{
    [UsedImplicitly]
    public class MechJebModuleWarpHelper : DisplayModule
    {
        public enum WarpTarget { Periapsis, Apoapsis, Node, SoI, Time, PhaseAngleT, SuicideBurn, AtmosphericEntry }

        private static readonly string[] warpTargetStrings =
        {
            Localizer.Format("#MechJeb_WarpHelper_Combobox_text1"), Localizer.Format("#MechJeb_WarpHelper_Combobox_text2"),
            Localizer.Format("#MechJeb_WarpHelper_Combobox_text3"), Localizer.Format("#MechJeb_WarpHelper_Combobox_text4"),
            Localizer.Format("#MechJeb_WarpHelper_Combobox_text5"), Localizer.Format("#MechJeb_WarpHelper_Combobox_text6"),
            Localizer.Format("#MechJeb_WarpHelper_Combobox_text7"), Localizer.Format("#MechJeb_WarpHelper_Combobox_text8")
        }; //"periapsis""apoapsis""maneuver node""SoI transition""Time""Phase angle""suicide burn""atmospheric entry"

        [Persistent(pass = (int)Pass.Global)]
        public WarpTarget warpTarget = WarpTarget.Periapsis;

        [Persistent(pass = (int)Pass.Global)]
        public EditableTime leadTime = 0;

        public           bool         warping;
        private readonly EditableTime timeOffset = 0;

        private double targetUT;

        [Persistent(pass = (int)(Pass.Local | Pass.Type | Pass.Global))]
        private readonly EditableDouble phaseAngle = 0;

        protected override void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("#MechJeb_WarpHelper_label1"), GUILayout.ExpandWidth(false)); //"Warp to: "
            warpTarget = (WarpTarget)GuiUtils.ComboBox.Box((int)warpTarget, warpTargetStrings, this);
            GUILayout.EndHorizontal();

            if (warpTarget == WarpTarget.Time)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Localizer.Format("#MechJeb_WarpHelper_label2"), GUILayout.ExpandWidth(true)); //"Warp for: "
                timeOffset.text = GUILayout.TextField(timeOffset.text, GUILayout.Width(100));
                GUILayout.EndHorizontal();
            }
            else if (warpTarget == WarpTarget.PhaseAngleT)
            {
                // I wonder if I should check for target that don't make sense
                if (!core.Target.NormalTargetExists)
                    GUILayout.Label(Localizer.Format("#MechJeb_WarpHelper_label3")); //"You need a target"
                else
                    GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_WarpHelper_label4"), phaseAngle, "º", 60); //"Phase Angle:"
            }

            GUILayout.BeginHorizontal();

            GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_WarpHelper_label5"), leadTime, ""); //"Lead time: "

            if (warping)
            {
                if (GUILayout.Button(Localizer.Format("#MechJeb_WarpHelper_button1"))) //"Abort"
                {
                    warping = false;
                    core.Warp.MinimumWarp(true);
                }
            }
            else
            {
                if (GUILayout.Button(Localizer.Format("#MechJeb_WarpHelper_button2"))) //"Warp"
                {
                    warping = true;

                    switch (warpTarget)
                    {
                        case WarpTarget.Periapsis:
                            targetUT = orbit.NextPeriapsisTime(vesselState.time);
                            break;

                        case WarpTarget.Apoapsis:
                            if (orbit.eccentricity < 1) targetUT = orbit.NextApoapsisTime(vesselState.time);
                            break;

                        case WarpTarget.SoI:
                            if (orbit.patchEndTransition != Orbit.PatchTransitionType.FINAL) targetUT = orbit.EndUT;
                            break;

                        case WarpTarget.Node:
                            if (vessel.patchedConicsUnlocked() && vessel.patchedConicSolver.maneuverNodes.Any())
                                targetUT = vessel.patchedConicSolver.maneuverNodes[0].UT;
                            break;

                        case WarpTarget.Time:
                            targetUT = vesselState.time + timeOffset;
                            break;

                        case WarpTarget.PhaseAngleT:
                            if (core.Target.NormalTargetExists)
                            {
                                Orbit reference;
                                if (core.Target.TargetOrbit.referenceBody == orbit.referenceBody)
                                    reference = orbit; // we orbit arround the same body
                                else
                                    reference = orbit.referenceBody.orbit;
                                // From Kerbal Alarm Clock
                                double angleChangePerSec = 360 / core.Target.TargetOrbit.period - 360 / reference.period;
                                double currentAngle = reference.PhaseAngle(core.Target.TargetOrbit, vesselState.time);
                                double angleDigff = currentAngle - phaseAngle;
                                if (angleDigff > 0 && angleChangePerSec > 0)
                                    angleDigff -= 360;
                                if (angleDigff < 0 && angleChangePerSec < 0)
                                    angleDigff += 360;
                                double TimeToTarget = Math.Floor(Math.Abs(angleDigff / angleChangePerSec));
                                targetUT = vesselState.time + TimeToTarget;
                            }

                            break;

                        case WarpTarget.AtmosphericEntry:
                            try
                            {
                                targetUT = vessel.orbit.NextTimeOfRadius(vesselState.time,
                                    vesselState.mainBody.Radius + vesselState.mainBody.RealMaxAtmosphereAltitude());
                            }
                            catch
                            {
                                warping = false;
                            }

                            break;

                        case WarpTarget.SuicideBurn:
                            try
                            {
                                targetUT = OrbitExtensions.SuicideBurnCountdown(orbit, vesselState, vessel) + vesselState.time;
                            }
                            catch
                            {
                                warping = false;
                            }

                            break;

                        default:
                            targetUT = vesselState.time;
                            break;
                    }
                }
            }

            GUILayout.EndHorizontal();

            core.Warp.useQuickWarpInfoItem();

            if (warping)
                GUILayout.Label(Localizer.Format("#MechJeb_WarpHelper_label6") + (leadTime > 0 ? GuiUtils.TimeToDHMS(leadTime) + " before " : "") +
                                warpTargetStrings[(int)warpTarget] + "."); //"Warping to "

            core.Warp.ControlWarpButton();

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }

        public override void OnFixedUpdate()
        {
            if (!warping) return;

            if (warpTarget == WarpTarget.SuicideBurn)
            {
                try
                {
                    targetUT = OrbitExtensions.SuicideBurnCountdown(orbit, vesselState, vessel) + vesselState.time;
                }
                catch
                {
                    warping = false;
                }
            }

            double target = targetUT - leadTime;

            if (target < vesselState.time + 1)
            {
                core.Warp.MinimumWarp(true);
                warping = false;
            }
            else
            {
                core.Warp.WarpToUT(target);
            }
        }

        public override GUILayoutOption[] WindowOptions()
        {
            return new[] { GUILayout.Width(240), GUILayout.Height(50) };
        }

        public override bool isActive()
        {
            return warping;
        }

        public override string GetName()
        {
            return Localizer.Format("#MechJeb_WarpHelper_title"); //"Warp Helper"
        }

        public override string IconName()
        {
            return "Warp Helper";
        }

        public MechJebModuleWarpHelper(MechJebCore core) : base(core) { }
    }
}
