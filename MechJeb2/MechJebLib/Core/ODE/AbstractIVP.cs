﻿/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: MIT-0 OR LGPL-2.1+ OR CC0-1.0
 */

using System;
using System.Collections.Generic;
using System.Threading;
using MechJebLib.Primitives;
using static MechJebLib.Utils.Statics;

#nullable enable

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace MechJebLib.Core.ODE
{
    using IVPFunc = Action<Vn, double, Vn>;
    using IVPEvent = Func<double, Vn, Vn, (double x, bool dir, bool stop)>;

    public abstract class AbstractIVP
    {
        /// <summary>
        ///     Minimum h step (may be violated on the last step or before an event).
        /// </summary>
        public double Hmin { get; set; } = EPS;

        /// <summary>
        ///     Maximum h step.
        /// </summary>
        public double Hmax { get; set; }

        /// <summary>
        ///     Maximum number of steps.
        /// </summary>
        public double Maxiter { get; set; } = 2000;

        /// <summary>
        ///     Desired local accuracy.
        /// </summary>
        public double Accuracy { get; set; } = 1e-9;

        /// <summary>
        ///     Starting step-size (can be zero for automatic guess).
        /// </summary>
        public double Hstart { get; set; }

        /// <summary>
        ///     Interpolants are pulled on an evenly spaced grid
        /// </summary>
        public int Interpnum { get; set; } = 20;

        /// <summary>
        ///     Throw exception when MaxIter is hit (PVG optimizer works better with this set to false).
        /// </summary>
        public bool ThrowOnMaxIter { get; set; } = true;

        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        ///     Dormand Prince 5(4)7FM ODE integrator (aka DOPRI5 aka ODE45)
        /// </summary>
        /// <param name="f"></param>
        /// <param name="y0"></param>
        /// <param name="yf"></param>
        /// <param name="t0"></param>
        /// <param name="tf"></param>
        /// <param name="interpolant"></param>
        /// <param name="events"></param>
        /// <exception cref="ArgumentException"></exception>
        public void Solve(IVPFunc f, IReadOnlyList<double> y0, IList<double> yf, double t0, double tf, Hn? interpolant = null,
            List<IVPEvent>? events = null)
        {
            int n = y0.Count;

            using var ynew = Vn.Rent(n);
            using var dynew = Vn.Rent(n);
            using var dy = Vn.Rent(n);
            using var y = Vn.Rent(n);
            using IDisposable data = SetupData(n);

            int direction = t0 != tf ? Math.Sign(tf - t0) : 1;
            double habs = SelectInitialStep(t0, tf);

            double t = t0;
            y.CopyFrom(y0);
            double niter = 0;
            int interpCount = 1;

            f(y, t, dy);

            interpolant?.Add(t, y, dy);

            while (t != tf)
            {
                CancellationToken.ThrowIfCancellationRequested();

                double h = habs * direction;

                double tnew = t + h;

                if (direction * (tnew - tf) > 0)
                    tnew = tf;

                h    = tnew - t;
                habs = Math.Abs(h);

                habs = Step(f, t, habs, direction, y, dy, ynew, dynew, data);

                if (events != null)
                {
                }

                // extract a low fidelity interpolant
                if (interpolant != null)
                {
                    while (interpCount < Interpnum)
                    {
                        double tinterp = t0 + (tf - t0) * interpCount / Interpnum;

                        if (!tinterp.IsWithin(t, tnew))
                            break;

                        using var yinterp = Vn.Rent(n);
                        using var finterp = Vn.Rent(n);

                        PrepareInterpolant(habs, direction, y, dy, ynew, dynew, data);
                        Interpolate(tinterp, t, h, y, yinterp, data);
                        f(yinterp, tinterp, finterp);
                        interpolant?.Add(tinterp, yinterp, finterp);
                        interpCount++;
                    }
                }

                // take a step
                ynew.CopyTo(y);
                dynew.CopyTo(dy);
                t = tnew;

                // handle max iterations
                if (Maxiter > 0 && niter++ > Maxiter)
                {
                    if (ThrowOnMaxIter)
                        throw new ArgumentException("maximum iterations exceeded");

                    break;
                }
            }

            interpolant?.Add(t, y, dy);

            y.CopyTo(yf);
        }

        protected abstract double      Step(IVPFunc f, double t, double habs, int direction, Vn y, Vn dy, Vn ynew, Vn dynew, object data);
        protected abstract double      SelectInitialStep(double t0, double tf);
        protected abstract void        PrepareInterpolant(double habs, int direction, Vn y, Vn dy, Vn ynew, Vn dynew, object data);
        protected abstract void        Interpolate(double x, double t, double h, Vn y, Vn yout, object data);
        protected abstract IDisposable SetupData(int n);
    }
}
