// Ported from:
// https://github.com/WebKit/WebKit/blob/main/Source/WebCore/platform/graphics/UnitBezier.h
/*
 * Copyright (C) 2008 Apple Inc. All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY APPLE INC. ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 * PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL APPLE INC. OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
 * OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. 
 */

namespace Zen.Scroll;

internal sealed class UnitBezier
{
    private const int CUBIC_BEZIER_SPLINE_SAMPLES = 11;
    private const double BezierEpsilon = 1e-7;
    private const int MaxNewtonIterations = 4;


    private double x1, x2, y1, y2;
    private double ax, bx, cx;
    private double ay, by, cy;
    private double startGradient, endGradient;
    private readonly double[] splineSamples; // 固定大小，构造时分配一次

    public double X1 => x1;

    public double X2 => x2;

    public double Y1 => y1;

    public double Y2 => y2;

    public UnitBezier(double p1x, double p1y, double p2x, double p2y)
    {
        splineSamples = new double[CUBIC_BEZIER_SPLINE_SAMPLES];
        SetParameters(p1x, p1y, p2x, p2y);
    }

    /// <summary>
    /// 重新设定贝塞尔参数，同时重新计算所有内部系数和样本。
    /// </summary>
    public void SetParameters(double p1x, double p1y, double p2x, double p2y)
    {
        x1 = p1x; x2 = p2x;
        y1 = p1y; y2 = p2y;

        // 计算多项式系数
        cx = 3.0 * p1x;
        bx = 3.0 * (p2x - p1x) - cx;
        ax = 1.0 - cx - bx;

        cy = 3.0 * p1y;
        by = 3.0 * (p2y - p1y) - cy;
        ay = 1.0 - cy - by;

        // 计算端点梯度
        if (p1x > 0)
            startGradient = p1y / p1x;
        else if (p1y == 0 && p2x > 0)
            startGradient = p2y / p2x;
        else if (p1y == 0 && p2y == 0)
            startGradient = 1;
        else
            startGradient = 0;

        if (p2x < 1)
            endGradient = (p2y - 1) / (p2x - 1);
        else if (p2y == 1 && p1x < 1)
            endGradient = (p1y - 1) / (p1x - 1);
        else if (p2y == 1 && p1y == 1)
            endGradient = 1;
        else
            endGradient = 0;

        // 重新填充样条采样点
        double deltaT = 1.0 / (CUBIC_BEZIER_SPLINE_SAMPLES - 1);
        for (int i = 0; i < CUBIC_BEZIER_SPLINE_SAMPLES; i++)
            splineSamples[i] = SampleCurveX(i * deltaT);
    }

    public double SampleCurveX(double t)
    {
        return ((ax * t + bx) * t + cx) * t;
    }

    public double SampleCurveY(double t)
    {
        return ((ay * t + by) * t + cy) * t;
    }

    public double SampleCurveDerivativeX(double t)
    {
        return (3.0 * ax * t + 2.0 * bx) * t + cx;
    }

    public double SolveCurveX(double x, double epsilon)
    {
        double t0 = 0.0;
        double t1 = 0.0;
        double t2 = x;
        double x2 = 0.0;
        int i;

        double deltaT = 1.0 / (CUBIC_BEZIER_SPLINE_SAMPLES - 1);
        for (i = 1; i < CUBIC_BEZIER_SPLINE_SAMPLES; i++)
        {
            if (x <= splineSamples[i])
            {
                t1 = deltaT * i;
                t0 = t1 - deltaT;
                t2 = t0 + (t1 - t0) * (x - splineSamples[i - 1]) / (splineSamples[i] - splineSamples[i - 1]);
                break;
            }
        }

        double newtonEpsilon = Math.Min(BezierEpsilon, epsilon);
        for (i = 0; i < MaxNewtonIterations; i++)
        {
            x2 = SampleCurveX(t2) - x;
            if (Math.Abs(x2) < newtonEpsilon)
                return t2;
            double d2 = SampleCurveDerivativeX(t2);
            if (Math.Abs(d2) < BezierEpsilon)
                break;
            t2 -= x2 / d2;
        }
        if (Math.Abs(x2) < epsilon)
            return t2;

        while (t0 < t1)
        {
            x2 = SampleCurveX(t2);
            if (Math.Abs(x2 - x) < epsilon)
                return t2;
            if (x > x2)
                t0 = t2;
            else
                t1 = t2;
            t2 = (t1 + t0) * 0.5;
        }

        return t2;
    }

    public double Solve(double x, double epsilon = BezierEpsilon)
    {
        if (x < 0.0)
            return 0.0 + startGradient * x;
        if (x > 1.0)
            return 1.0 + endGradient * (x - 1.0);
        return SampleCurveY(SolveCurveX(x, epsilon));
    }
}