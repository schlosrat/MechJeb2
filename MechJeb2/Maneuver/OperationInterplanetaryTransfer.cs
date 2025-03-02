﻿extern alias JetBrainsAnnotations;
using System.Collections.Generic;
using JetBrainsAnnotations::JetBrains.Annotations;
using KSP.Localization;
using UnityEngine;

namespace MuMech
{
    [UsedImplicitly]
    public class OperationInterplanetaryTransfer : Operation
    {
        private static readonly string _name = Localizer.Format("#MechJeb_transfer_title");
        public override         string GetName() => _name;

        [UsedImplicitly]
        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool WaitForPhaseAngle = true;

        public override void DoParametersGUI(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            GUILayout.Label(Localizer.Format("#MechJeb_transfer_Label1"));                                           //Schedule the burn:
            WaitForPhaseAngle = GUILayout.Toggle(WaitForPhaseAngle, Localizer.Format("#MechJeb_transfer_Label2"));   //at the next transfer window.
            WaitForPhaseAngle = !GUILayout.Toggle(!WaitForPhaseAngle, Localizer.Format("#MechJeb_transfer_Label3")); //as soon as possible

            if (!WaitForPhaseAngle)
            {
                GUILayout.Label(Localizer.Format("#MechJeb_transfer_Label4"), GuiUtils.YellowLabel); //Using this mode voids your warranty
            }
        }

        protected override List<ManeuverParameters> MakeNodesImpl(Orbit o, double UT, MechJebModuleTargetController target)
        {
            // Check preconditions
            if (!target.NormalTargetExists)
                throw new OperationException(
                    Localizer.Format("#MechJeb_transfer_Exception1")); //"must select a target for the interplanetary transfer."

            if (o.referenceBody.referenceBody == null)
                throw new OperationException(Localizer.Format("#MechJeb_transfer_Exception2",
                    o.referenceBody.displayName
                        .LocalizeRemoveGender())); //doesn't make sense to plot an interplanetary transfer from an orbit around <<1>>

            if (o.referenceBody.referenceBody != target.TargetOrbit.referenceBody)
            {
                if (o.referenceBody == target.TargetOrbit.referenceBody)
                    throw new OperationException(Localizer.Format("#MechJeb_transfer_Exception3",
                        o.referenceBody.displayName
                            .LocalizeRemoveGender())); //use regular Hohmann transfer function to intercept another body orbiting <<1>>
                throw new OperationException(Localizer.Format("#MechJeb_transfer_Exception4", o.referenceBody.displayName.LocalizeRemoveGender(),
                    o.referenceBody.displayName.LocalizeRemoveGender(),
                    o.referenceBody.referenceBody.displayName
                        .LocalizeRemoveGender())); //"an interplanetary transfer from within "<<1>>"'s sphere of influence must target a body that orbits "<<2>>"'s parent, "<<3>>.
            }

            // Simple warnings
            if (o.referenceBody.orbit.RelativeInclination(target.TargetOrbit) > 30)
            {
                ErrorMessage = Localizer.Format("#MechJeb_transfer_errormsg1", o.RelativeInclination(target.TargetOrbit).ToString("F0"),
                    o.referenceBody.displayName
                        .LocalizeRemoveGender()); //"Warning: target's orbital plane is at a"<<1>>"º angle to "<<2>>"'s orbital plane (recommend at most 30º). Planned interplanetary transfer may not intercept target properly."
            }
            else
            {
                double relativeInclination = Vector3d.Angle(o.OrbitNormal(), o.referenceBody.orbit.OrbitNormal());
                if (relativeInclination > 10)
                {
                    ErrorMessage = Localizer.Format("#MechJeb_transfer_errormsg2", o.referenceBody.displayName.LocalizeRemoveGender(),
                        o.referenceBody.displayName.LocalizeRemoveGender(), o.referenceBody.referenceBody.displayName.LocalizeRemoveGender(),
                        o.referenceBody.displayName.LocalizeRemoveGender(), relativeInclination.ToString("F1"),
                        o.referenceBody.displayName.LocalizeRemoveGender(),
                        o.referenceBody.referenceBody.displayName
                            .LocalizeRemoveGender()); //Warning: Recommend starting interplanetary transfers from  <<1>> from an orbit in the same plane as "<<2>>"'s orbit around "<<3>>". Starting orbit around "<<4>>" is inclined "<<5>>"º with respect to "<<6>>"'s orbit around "<<7>> " (recommend < 10º). Planned transfer may not intercept target properly."
                }
                else if (o.eccentricity > 0.2)
                {
                    ErrorMessage = Localizer.Format("#MechJeb_transfer_errormsg3",
                        o.eccentricity.ToString(
                            "F2")); //Warning: Recommend starting interplanetary transfers from a near-circular orbit (eccentricity < 0.2). Planned transfer is starting from an orbit with eccentricity <<1>> and so may not intercept target properly.
                }
            }

            Vector3d dV = OrbitalManeuverCalculator.DeltaVAndTimeForInterplanetaryTransferEjection(o, UT, target.TargetOrbit, WaitForPhaseAngle,
                out UT);

            return new List<ManeuverParameters> { new ManeuverParameters(dV, UT) };
        }
    }
}
