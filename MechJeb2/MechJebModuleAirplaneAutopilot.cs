extern alias JetBrainsAnnotations;
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

// ReSharper disable MemberCanBePrivate.Global

namespace MuMech
{
    [SuppressMessage("Minor Code Smell", "S1104:Fields should not have public accessibility")]
    public class MechJebModuleAirplaneAutopilot : ComputerModule
    {
        [Persistent(pass = (int)Pass.LOCAL)]
        public bool HeadingHoldEnabled, AltitudeHoldEnabled, VertSpeedHoldEnabled, RollHoldEnabled, SpeedHoldEnabled;

        [Persistent(pass = (int)Pass.LOCAL)]
        public double AltitudeTarget = 0, HeadingTarget = 90, RollTarget = 0, SpeedTarget = 0, VertSpeedTarget = 0;

        [Persistent(pass = (int)Pass.LOCAL)]
        public double BankAngle = 30;

        [Persistent(pass = (int)Pass.LOCAL)]
        public readonly EditableDouble AccKp = 0.5, AccKi = 0.5, AccKd = 0.005;

        [Persistent(pass = (int)Pass.LOCAL)]
        public readonly EditableDouble PitKp = 2.0, PitKi = 1.0, PitKd = 0.005;

        [Persistent(pass = (int)Pass.LOCAL)]
        public readonly EditableDouble RolKp = 0.5, RolKi = 0.02, RolKd = 0.5;

        [Persistent(pass = (int)Pass.LOCAL)]
        public readonly EditableDouble YawKp = 1.0, YawKi = 0.25, YawKd = 0.02;

        [Persistent(pass = (int)Pass.LOCAL)]
        public readonly EditableDouble YawLimit = 10, RollLimit = 45, PitchDownLimit = 15, PitchUpLimit = 25;

        public PIDController AccelerationPIDController, PitchPIDController, RollPIDController, YawPIDController;

        public double AErr, CurAcc, Spd;
        public double CurrYaw;
        public double PitchErr,            RollErr,         YawErr;
        public double PitchAct,            RollAct,         YawAct;
        public double RealVertSpeedTarget, RealPitchTarget, RealRollTarget, RealYawTarget, RealAccelerationTarget;

        private bool _initPitchController;
        private bool _initRollController;
        private bool _initYawController;

        public MechJebModuleAirplaneAutopilot(MechJebCore core) : base(core)
        {
        }

        public override void OnStart(PartModule.StartState state)
        {
            AccelerationPIDController = new PIDController(AccKp, AccKi, AccKd);
            PitchPIDController        = new PIDController(PitKp, PitKi, PitKd);
            RollPIDController         = new PIDController(RolKp, RolKi, RolKd);
            YawPIDController          = new PIDController(YawKp, YawKi, YawKd);
        }

        protected override void OnModuleEnabled()
        {
            if (AltitudeHoldEnabled)
                EnableAltitudeHold();
            if (SpeedHoldEnabled)
                EnableSpeedHold();
        }

        protected override void OnModuleDisabled()
        {
            if (SpeedHoldEnabled)
            {
                Core.Thrust.Users.Remove(this);
            }
        }

        public void EnableHeadingHold()
        {
            HeadingHoldEnabled  = true;
            RollHoldEnabled     = true;
            _initRollController = true;
            _initYawController  = true;
            YawPIDController.Reset();
            RollPIDController.Reset();
        }

        public void DisableHeadingHold()
        {
            HeadingHoldEnabled = false;
            RollHoldEnabled    = false;
        }

        public void EnableSpeedHold()
        {
            if (!Enabled)
                return;

            Spd              = VesselState.speedSurface;
            SpeedHoldEnabled = true;
            Core.Thrust.Users.Add(this);
            AccelerationPIDController.Reset();
        }

        public void DisableSpeedHold()
        {
            if (!Enabled)
                return;

            SpeedHoldEnabled = false;
            Core.Thrust.Users.Remove(this);
        }

        public void EnableVertSpeedHold()
        {
            if (!Enabled)
                return;

            VertSpeedHoldEnabled = true;
            PitchPIDController.Reset();
            _initPitchController = true;
        }

        public void DisableVertSpeedHold()
        {
            if (!Enabled)
                return;

            VertSpeedHoldEnabled = false;
        }

        public void EnableAltitudeHold()
        {
            if (!Enabled)
                return;

            AltitudeHoldEnabled = true;
            if (VertSpeedHoldEnabled)
            {
                PitchPIDController.Reset();
            }
            else
            {
                EnableVertSpeedHold();
            }
        }

        public void DisableAltitudeHold()
        {
            if (!Enabled)
                return;

            AltitudeHoldEnabled = false;
            DisableVertSpeedHold();
        }

        private double convertAltitudeToVerticalSpeed(double deltaAltitude, double reference = 5)
        {
            if (reference < 2)
                reference = 2;

            if (deltaAltitude > reference || deltaAltitude < -reference)
                return deltaAltitude / reference;
            if (deltaAltitude > 0.1)
                return Math.Max(0.05, deltaAltitude * deltaAltitude / (reference * reference));
            if (deltaAltitude < -0.1)
                return Math.Min(-0.05, -deltaAltitude * deltaAltitude / (reference * reference));
            return 0;
        }

        private void UpdatePID()
        {
            AccelerationPIDController.Kp = AccKp;
            AccelerationPIDController.Ki = AccKi;
            AccelerationPIDController.Kd = AccKd;
            PitchPIDController.Kp        = PitKp;
            PitchPIDController.Ki        = PitKi;
            PitchPIDController.Kd        = PitKd;
            RollPIDController.Kp         = RolKp;
            RollPIDController.Ki         = RolKi;
            RollPIDController.Kd         = RolKd;
            YawPIDController.Kp          = YawKp;
            YawPIDController.Ki          = YawKi;
            YawPIDController.Kd          = YawKd;
        }

        private void SynchronizePIDs(FlightCtrlState s)
        {
            // synchronize PID controllers
            if (_initPitchController)
            {
                PitchPIDController.INTAccum = s.pitch * 100 / PitchPIDController.Ki;
            }

            if (_initRollController)
            {
                RollPIDController.INTAccum = s.roll * 100 / RollPIDController.Ki;
            }

            if (_initYawController)
            {
                YawPIDController.INTAccum = s.yaw * 100 / YawPIDController.Ki;
            }

            _initPitchController = false;
            _initRollController  = false;
            _initYawController   = false;
        }

        private double ComputeYaw()
        {
            // probably not perfect, especially when the aircraft is turning
            double path = VesselState.HeadingFromDirection(VesselState.surfaceVelocity);
            double nose = VesselState.HeadingFromDirection(VesselState.forward);
            double angle = MuUtils.ClampDegrees180(nose - path);

            return angle;
        }

        public override void Drive(FlightCtrlState s)
        {
            UpdatePID();
            SynchronizePIDs(s);

            //SpeedHold (set AccelerationTarget automatically to hold speed)
            if (SpeedHoldEnabled)
            {
                double spd = VesselState.speedSurface;
                CurAcc                             = (spd - Spd) / Time.fixedDeltaTime;
                Spd                                = spd;
                RealAccelerationTarget             = (SpeedTarget - spd) / 4;
                AErr                               = RealAccelerationTarget - CurAcc;
                AccelerationPIDController.INTAccum = MuUtils.Clamp(AccelerationPIDController.INTAccum, -1 / AccKi, 1 / AccKi);
                double tAct = AccelerationPIDController.Compute(AErr);
                if (!double.IsNaN(tAct))
                {
                    Core.Thrust.TargetThrottle = (float)MuUtils.Clamp(tAct, 0, 1);
                }
                else
                {
                    Core.Thrust.TargetThrottle = 0.0f;
                    AccelerationPIDController.Reset();
                }
            }

            //AltitudeHold (set VertSpeed automatically to hold altitude)
            if (AltitudeHoldEnabled)
            {
                // NOTE:
                // There is about 0.4 between altitudeASL and the altitude that is displayed in KSP in the top center
                // i.e. 3000.4m (altitudeASL) equals 3000m ingame.
                // maybe there is something wrong with the way we calculate altitudeASL. Idk
                double deltaAltitude = AltitudeTarget + 0.4 - VesselState.altitudeASL;

                RealVertSpeedTarget = convertAltitudeToVerticalSpeed(deltaAltitude, 10);
                RealVertSpeedTarget = UtilMath.Clamp(RealVertSpeedTarget, -VertSpeedTarget, VertSpeedTarget);
            }
            else
                RealVertSpeedTarget = VertSpeedTarget;

            PitchErr = RollErr = YawErr = 0;
            PitchAct = RollAct = YawAct = 0;

            //VertSpeedHold
            if (VertSpeedHoldEnabled)
            {
                // NOTE: 60-to-1 rule:
                // deltaAltitude = 2 * PI * r * deltaPitch / 360
                // Vvertical = 2 * PI * TAS * deltaPitch / 360
                // deltaPitch = Vvertical / Vhorizontal * 180 / PI
                double deltaVertSpeed = RealVertSpeedTarget - VesselState.speedVertical;
                double adjustment = deltaVertSpeed / VesselState.speedSurface * 180 / Math.PI;

                RealPitchTarget = VesselState.vesselPitch + adjustment;

                RealPitchTarget = UtilMath.Clamp(RealPitchTarget, -PitchDownLimit, PitchUpLimit);
                PitchErr        = MuUtils.ClampDegrees180(RealPitchTarget - VesselState.vesselPitch);

                PitchPIDController.INTAccum = UtilMath.Clamp(PitchPIDController.INTAccum, -100 / PitKi, 100 / PitKi);
                PitchAct                    = PitchPIDController.Compute(PitchErr) / 100;

                if (double.IsNaN(PitchAct))
                {
                    PitchPIDController.Reset();
                }
                else
                {
                    s.pitch = Mathf.Clamp((float)PitchAct, -1, 1);
                }
            }

            CurrYaw = ComputeYaw();

            // NOTE: we can not use vesselState.vesselHeading here because it interpolates headings internally
            //       i.e. turning from 1 to 359 will end up as (1+359)/2 = 180
            //       see class MovingAverage for more details

            //HeadingHold
            if (HeadingHoldEnabled)
            {
                double toturn = MuUtils.ClampDegrees180(HeadingTarget - VesselState.currentHeading);

                if (Math.Abs(toturn) < 0.2)
                {
                    // yaw for small adjustments
                    RealYawTarget  = MuUtils.Clamp(toturn * 2, -YawLimit, YawLimit);
                    RealRollTarget = 0;
                }
                else
                {
                    // roll for large adjustments
                    RealYawTarget  = 0;
                    RealRollTarget = MuUtils.Clamp(toturn * 2, -RollLimit, RollLimit);
                }
            }
            else
            {
                RealRollTarget = RollTarget;
                RealYawTarget  = 0;
            }

            if (RollHoldEnabled)
            {
                RealRollTarget = UtilMath.Clamp(RealRollTarget, -BankAngle, BankAngle);
                RealRollTarget = UtilMath.Clamp(RealRollTarget, -RollLimit, RollLimit);
                RollErr        = MuUtils.ClampDegrees180(RealRollTarget - -VesselState.currentRoll);

                RollPIDController.INTAccum = MuUtils.Clamp(RollPIDController.INTAccum, -100 / RolKi, 100 / RolKi);
                RollAct                    = RollPIDController.Compute(RollErr) / 100;

                if (double.IsNaN(RollAct))
                    RollPIDController.Reset();
                else
                    s.roll = Mathf.Clamp((float)RollAct, -1, 1);
            }

            if (HeadingHoldEnabled)
            {
                RealYawTarget = UtilMath.Clamp(RealYawTarget, -YawLimit, YawLimit);
                YawErr        = MuUtils.ClampDegrees180(RealYawTarget - CurrYaw);

                YawPIDController.INTAccum = MuUtils.Clamp(YawPIDController.INTAccum, -100 / YawKi, 100 / YawKi);
                YawAct                    = YawPIDController.Compute(YawErr) / 100;

                if (double.IsNaN(YawAct))
                    YawPIDController.Reset();
                else
                    s.yaw = Mathf.Clamp((float)YawAct, -1, 1);
            }
        }
    }
}
