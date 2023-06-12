/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

#nullable enable

using MechJebLib.Primitives;
using MechJebLib.PVG.Terminal;

namespace MechJebLib.PVG
{
    public class Problem
    {
        public readonly Scale         Scale;
        public          IPVGTerminal? Terminal;
        public readonly V3            R0;
        public readonly V3            R0Bar;
        public readonly double        M0;
        public readonly double        M0Bar;
        public readonly V3            V0;
        public readonly V3            V0Bar;
        public readonly double        T0;
        public readonly V3            U0;
        public readonly double        Mu;
        public readonly double        Rbody;

        public Problem(V3 r0, V3 v0, V3 u0, double m0, double t0, double mu, double rbody)
        {
            Scale = Scale.Create(mu, r0.magnitude, m0);
            Mu    = mu;
            R0    = r0;
            R0Bar = r0 / Scale.LengthScale;
            V0    = v0;
            V0Bar = v0 / Scale.VelocityScale;
            M0    = m0;
            M0Bar = m0 / Scale.MassScale;
            U0    = u0;
            Rbody = rbody;

            T0 = t0;
        }
    }
}
