﻿using System;
using System.IO;
using JetBrains.Annotations;

namespace MuMech
{
    //A class to record flight data, currently deltaV and time
    //TODO: make records persistent
    [UsedImplicitly]
    public class MechJebModuleFlightRecorder : ComputerModule
    {
        // TODO : this is already nearly an array so use a list and allow to add any generic ValueInfoItem
        public struct RecordStruct
        {
            public double timeSinceMark;
            public int    currentStage;
            public double altitudeASL;
            public double downRange;
            public double speedSurface;
            public double speedOrbital;
            public double acceleration;
            public double Q;
            public double AoA;
            public double AoS;
            public double AoD;
            public double altitudeTrue;
            public double pitch;
            public double mass;
            public double gravityLosses;
            public double dragLosses;
            public double steeringLosses;
            public double deltaVExpended;

            public double this[recordType type]
            {
                get
                {
                    switch (type)
                    {
                        case recordType.TimeSinceMark:
                            return timeSinceMark;
                        case recordType.CurrentStage:
                            return currentStage;
                        case recordType.AltitudeASL:
                            return altitudeASL;
                        case recordType.DownRange:
                            return downRange;
                        case recordType.SpeedSurface:
                            return speedSurface;
                        case recordType.SpeedOrbital:
                            return speedOrbital;
                        case recordType.Mass:
                            return mass;
                        case recordType.Acceleration:
                            return acceleration;
                        case recordType.Q:
                            return Q;
                        case recordType.AoA:
                            return AoA;
                        case recordType.AoS:
                            return AoS;
                        case recordType.AoD:
                            return AoD;
                        case recordType.AltitudeTrue:
                            return altitudeTrue;
                        case recordType.Pitch:
                            return pitch;
                        case recordType.GravityLosses:
                            return gravityLosses;
                        case recordType.DragLosses:
                            return dragLosses;
                        case recordType.SteeringLosses:
                            return steeringLosses;
                        case recordType.DeltaVExpended:
                            return deltaVExpended;
                        default:
                            return 0;
                    }
                }
            }
        }

        public enum recordType
        {
            TimeSinceMark,
            CurrentStage,
            AltitudeASL,
            DownRange,
            SpeedSurface,
            SpeedOrbital,
            Mass,
            Acceleration,
            Q,
            AoA,
            AoS,
            AoD,
            AltitudeTrue,
            Pitch,
            GravityLosses,
            DragLosses,
            SteeringLosses,
            DeltaVExpended
        }

        public RecordStruct[] history = new RecordStruct[1];

        public int historyIdx = -1;

        [Persistent(pass = (int)Pass.GLOBAL)]
        public int historySize = 3000;

        [Persistent(pass = (int)Pass.GLOBAL)]
        public double precision = 0.2;

        [Persistent(pass = (int)Pass.GLOBAL)]
        public bool downrange = true;

        [Persistent(pass = (int)Pass.GLOBAL)]
        public bool realAtmo = false;

        [Persistent(pass = (int)Pass.GLOBAL)]
        public bool stages = false;

        [Persistent(pass = (int)Pass.GLOBAL)]
        public int hSize = 4;

        [Persistent(pass = (int)Pass.GLOBAL)]
        public int vSize = 2;

        private static readonly int typeCount = Enum.GetValues(typeof(recordType)).Length;

        public double[] maximums;
        public double[] minimums;

        private readonly bool paused = false;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_MarkUT", InfoItem.Category.Recorder, format = ValueInfoItem.TIME)] //Mark UT
        public double markUT;

        [ValueInfoItem("#MechJeb_TimeSinceMark", InfoItem.Category.Recorder, format = ValueInfoItem.TIME)] //Time since mark
        public double timeSinceMark;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_DVExpended", InfoItem.Category.Recorder, format = "F1", units = "m/s")] //ΔV expended
        public double deltaVExpended;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_DragLosses", InfoItem.Category.Recorder, format = "F1", units = "m/s")] //Drag losses
        public double dragLosses;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_GravityLosses", InfoItem.Category.Recorder, format = "F1", units = "m/s")] //Gravity losses
        public double gravityLosses;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_SteeringLosses", InfoItem.Category.Recorder, format = "F1", units = "m/s")] //Steering losses
        public double steeringLosses;

        [ValueInfoItem("#MechJeb_PhaseAngleFromMark", InfoItem.Category.Recorder, format = "F2", units = "º")] //Phase angle from mark
        public double phaseAngleFromMark;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_MarkLAN", InfoItem.Category.Recorder, format = ValueInfoItem.ANGLE_EW)] //Mark LAN
        public double markLAN;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_MarkLatitude", InfoItem.Category.Recorder, format = ValueInfoItem.ANGLE_NS)] //Mark latitude
        public double markLatitude;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_MarkLongitude", InfoItem.Category.Recorder, format = ValueInfoItem.ANGLE_EW)] //Mark longitude
        public double markLongitude;

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_MarkAltitudeASL", InfoItem.Category.Recorder, format = ValueInfoItem.SI, units = "m")] //Mark altitude ASL
        public double markAltitude;

        [Persistent(pass = (int)Pass.LOCAL)]
        public int markBodyIndex = 1;

        [ValueInfoItem("#MechJeb_MarkBody", InfoItem.Category.Recorder)] //Mark body
        public string MarkBody() => FlightGlobals.Bodies[markBodyIndex].bodyName;

        [ValueInfoItem("#MechJeb_DistanceFromMark", InfoItem.Category.Recorder, format = ValueInfoItem.SI, units = "m")] //Distance from mark
        public double DistanceFromMark() =>
            Vector3d.Distance(VesselState.CoM,
                FlightGlobals.Bodies[markBodyIndex].GetWorldSurfacePosition(markLatitude, markLongitude, markAltitude) -
                FlightGlobals.Bodies[markBodyIndex].position);

        [ValueInfoItem("#MechJeb_DownrangeDistance", InfoItem.Category.Recorder, format = ValueInfoItem.SI, units = "m")] //Downrange distance
        public double GroundDistanceFromMark()
        {
            CelestialBody markBody = FlightGlobals.Bodies[markBodyIndex];
            Vector3d markVector = markBody.GetSurfaceNVector(markLatitude, markLongitude);
            Vector3d vesselVector = VesselState.CoM - markBody.transform.position;
            return markBody.Radius * Vector3d.Angle(markVector, vesselVector) * UtilMath.Deg2Rad;
        }

        [Persistent(pass = (int)Pass.LOCAL)]
        [ValueInfoItem("#MechJeb_MaxDragGees", InfoItem.Category.Recorder, format = "F2")] //Max drag gees
        public double maxDragGees;

        [ActionInfoItem("MARK", InfoItem.Category.Recorder)]
        public void Mark()
        {
            markUT             = VesselState.time;
            deltaVExpended     = dragLosses = gravityLosses = steeringLosses = 0;
            phaseAngleFromMark = 0;
            markLatitude       = VesselState.latitude;
            markLongitude      = VesselState.longitude;
            markLAN            = VesselState.orbitLAN;
            markAltitude       = VesselState.altitudeASL;
            markBodyIndex      = FlightGlobals.Bodies.IndexOf(MainBody);
            maxDragGees        = 0;
            timeSinceMark      = 0;
            for (int t = 0; t < maximums.Length; t++)
            {
                minimums[t] = double.MaxValue;
                maximums[t] = double.MinValue;
            }

            historyIdx = 0;
            Record(historyIdx);
        }

        public MechJebModuleFlightRecorder(MechJebCore core)
            : base(core)
        {
            Priority = 2000;
            maximums = new double[typeCount];
            minimums = new double[typeCount];
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (history.Length != historySize)
                history = new RecordStruct[historySize];
            Users.Add(this); //flight recorder should always run.
        }

        private double lastRecordTime;

        public override void OnFixedUpdate()
        {
            if (markUT == 0) Mark();

            timeSinceMark = VesselState.time - markUT;

            if (Vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                Mark(); //keep resetting stats until we launch
                return;
            }

            if (VesselState.currentThrustAccel > 0)
            {
                gravityLosses += VesselState.deltaT * Vector3d.Dot(-VesselState.orbitalVelocity.normalized, VesselState.gravityForce);
            }

            dragLosses     += VesselState.deltaT * VesselState.drag;
            deltaVExpended += VesselState.deltaT * VesselState.currentThrustAccel;
            steeringLosses += VesselState.deltaT * VesselState.currentThrustAccel *
                              (1 - Vector3d.Dot(VesselState.orbitalVelocity.normalized, VesselState.forward));

            maxDragGees = Math.Max(maxDragGees, VesselState.drag / 9.81);

            double circularPeriod = Orbit.CircularOrbitPeriod();
            double angleTraversed = VesselState.longitude - markLongitude + 360 * (VesselState.time - markUT) / Part.vessel.mainBody.rotationPeriod;
            phaseAngleFromMark = MuUtils.ClampDegrees360(360 * (VesselState.time - markUT) / circularPeriod - angleTraversed);

            if (paused)
                return;

            //int oldHistoryIdx = historyIdx;

            //historyIdx = Mathf.Min(Mathf.FloorToInt((float)(timeSinceMark / precision)), history.Length - 1);

            if (VesselState.time >= lastRecordTime + precision && historyIdx < history.Length - 1)
            {
                lastRecordTime = VesselState.time;
                historyIdx++;
                Record(historyIdx);
                //if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                //    print("WRP " + historyIdx + " " + history[historyIdx].downRange.ToString("F0") + " " + history[historyIdx].AoA.ToString("F2"));
                //else
                //{
                //    print("STD " + historyIdx + " " + history[historyIdx].downRange.ToString("F0") + " " + history[historyIdx].AoA.ToString("F2"));
                //}
            }
        }

        private void Record(int idx)
        {
            history[idx].timeSinceMark  = timeSinceMark;
            history[idx].altitudeASL    = VesselState.altitudeASL;
            history[idx].downRange      = GroundDistanceFromMark();
            history[idx].speedSurface   = VesselState.speedSurface;
            history[idx].speedOrbital   = VesselState.speedOrbital;
            history[idx].acceleration   = Vessel.geeForce;
            history[idx].Q              = VesselState.dynamicPressure;
            history[idx].altitudeTrue   = VesselState.altitudeTrue;
            history[idx].pitch          = VesselState.vesselPitch;
            history[idx].mass           = VesselState.mass;
            history[idx].gravityLosses  = gravityLosses;
            history[idx].dragLosses     = dragLosses;
            history[idx].steeringLosses = steeringLosses;
            history[idx].deltaVExpended = deltaVExpended;

            if (TimeWarp.WarpMode != TimeWarp.Modes.HIGH)
            {
                history[idx].currentStage = Vessel.currentStage;
            }
            else
            {
                history[idx].currentStage = idx > 0 ? history[idx - 1].currentStage : Vessel.currentStage;
            }

            history[idx].AoA = VesselState.AoA;
            history[idx].AoS = VesselState.AoS;
            history[idx].AoD = VesselState.displacementAngle;
            for (int t = 0; t < typeCount; t++)
            {
                double current = history[idx][(recordType)t];
                minimums[t] = Math.Min(minimums[t], current);
                maximums[t] = Math.Max(maximums[t], current);
            }
        }

        public void DumpCSV()
        {
            string exportPath = KSPUtil.ApplicationRootPath + "/GameData/MechJeb2/Export/";

            if (!Directory.Exists(exportPath))
                Directory.CreateDirectory(exportPath);

            string vesselName = Vessel != null ? string.Join("_", Vessel.vesselName.Split(Path.GetInvalidFileNameChars())) : "";

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            string path = exportPath + vesselName + "_" + timestamp + ".csv";
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine(string.Join(",", Enum.GetNames(typeof(recordType))));

                for (int idx = 0; idx <= historyIdx; idx++)
                {
                    RecordStruct r = history[idx];
                    writer.Write(r[0]);
                    for (int i = 1; i < typeCount; i++)
                    {
                        writer.Write(',');
                        writer.Write(r[(recordType)i]);
                    }

                    writer.WriteLine();
                }
            }

            ScreenMessages.PostScreenMessage("Exported as\n" + path, 5);
        }
    }
}
