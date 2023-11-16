﻿extern alias JetBrainsAnnotations;
using JetBrainsAnnotations::JetBrains.Annotations;
using KSP.Localization;
using UnityEngine;

namespace MuMech
{
    [UsedImplicitly]
    public class MechJebModuleDeployableAntennaController : MechJebModuleDeployableController
    {
        public MechJebModuleDeployableAntennaController(MechJebCore core) : base(core)
        {
        }

        [GeneralInfoItem("#MechJeb_ToggleAntennas", InfoItem.Category.Misc, showInEditor = false)] //Toggle antennas
        public void AntennaDeployButton()
        {
            autoDeploy = GUILayout.Toggle(autoDeploy, Localizer.Format("#MechJeb_Autodeployantennas")); //"Auto-deploy antennas"

            if (GUILayout.Button(buttonText))
            {
                if (ExtendingOrRetracting())
                    return;

                if (!extended)
                    ExtendAll();
                else
                    RetractAll();
            }
        }

        protected override bool isModules(ModuleDeployablePart p) => p is ModuleDeployableAntenna;

        protected override string getButtonText(DeployablePartState deployablePartState)
        {
            switch (deployablePartState)
            {
                case DeployablePartState.EXTENDED:
                    return Localizer.Format("#MechJeb_AntennasEXTENDED"); //"Toggle antennas (currently extended)"
                case DeployablePartState.RETRACTED:
                    return Localizer.Format("#MechJeb_AntennasRETRACTED"); //"Toggle antennas (currently retracted)"
                default:
                    return Localizer.Format("#MechJeb_AntennasToggle"); //"Toggle antennas"
            }
        }
    }
}
