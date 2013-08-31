﻿// -----------------------------------------------------------------------
// <copyright file="Tron.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace Liblinear
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
/**
 * Trust Region Newton Method optimization
 */
class Tron {


    private readonly IFunction fun_obj;


    private readonly double   eps;


    private readonly int      max_iter;


    public Tron(IFunction fun_obj ):this(fun_obj, 0.1) {
        
    }


    public Tron(IFunction fun_obj, double eps ):this(fun_obj, eps, 1000) {
        
    }


    public Tron(IFunction fun_obj, double eps, int max_iter ) {
        this.fun_obj = fun_obj;
        this.eps = eps;
        this.max_iter = max_iter;
    }


    public void tron(double[] w) {
        // Parameters for updating the iterates.
        double eta0 = 1e-4, eta1 = 0.25, eta2 = 0.75;


        // Parameters for updating the trust region size delta.
        double sigma1 = 0.25, sigma2 = 0.5, sigma3 = 4;


        int n = fun_obj.get_nr_variable();
        int i, cg_iter;
        double delta, snorm, one = 1.0;
        double alpha, f, fnew, prered, actred, gs;
        int search = 1, iter = 1;
        double[] s = new double[n];
        double[] r = new double[n];
        double[] w_new = new double[n];
        double[] g = new double[n];


        for (i = 0; i < n; i++)
            w[i] = 0;


        f = fun_obj.fun(w);
        fun_obj.grad(w, g);
        delta = euclideanNorm(g);
        double gnorm1 = delta;
        double gnorm = gnorm1;


        if (gnorm <= eps * gnorm1) search = 0;


        iter = 1;


        while (iter <= max_iter && search != 0) {
            cg_iter = trcg(delta, g, s, r);


            Array.Copy(w, 0, w_new, 0, n);
            daxpy(one, s, w_new);


            gs = dot(g, s);
            prered = -0.5 * (gs - dot(s, r));
            fnew = fun_obj.fun(w_new);


            // Compute the actual reduction.
            actred = f - fnew;


            // On the first iteration, adjust the initial step bound.
            snorm = euclideanNorm(s);
            if (iter == 1) delta = Math.Min(delta, snorm);


            // Compute prediction alpha*snorm of the step.
            if (fnew - f - gs <= 0)
                alpha = sigma3;
            else
                alpha = Math.Max(sigma1, -0.5 * (gs / (fnew - f - gs)));


            // Update the trust region bound according to the ratio of actual to
            // predicted reduction.
            if (actred < eta0 * prered)
                delta = Math.Min(Math.Max(alpha, sigma1) * snorm, sigma2 * delta);
            else if (actred < eta1 * prered)
                delta = Math.Max(sigma1 * delta, Math.Min(alpha * snorm, sigma2 * delta));
            else if (actred < eta2 * prered)
                delta = Math.Max(sigma1 * delta, Math.Min(alpha * snorm, sigma3 * delta));
            else
                delta = Math.Max(delta, Math.Min(alpha * snorm, sigma3 * delta));


            Linear.info("iter {0} act {1} pre {2} delta {3} f {4} |g| {5} CG {6}", iter, actred, prered, delta, f, gnorm, cg_iter);


            if (actred > eta0 * prered) {
                iter++;
                Array.Copy(w_new, 0, w, 0, n);
                f = fnew;
                fun_obj.grad(w, g);


                gnorm = euclideanNorm(g);
                if (gnorm <= eps * gnorm1) break;
            }
            if (f < -1.0e+32) {
                Linear.info("WARNING: f < -1.0e+32");
                break;
            }
            if (Math.Abs(actred) <= 0 && prered <= 0) {
                Linear.info("WARNING: actred and prered <= 0");
                break;
            }
            if (Math.Abs(actred) <= 1.0e-12 * Math.Abs(f) && Math.Abs(prered) <= 1.0e-12 * Math.Abs(f)) {
                Linear.info("WARNING: actred and prered too small");
                break;
            }
        }
    }


    private int trcg(double delta, double[] g, double[] s, double[] r) {
        int n = fun_obj.get_nr_variable();
        double one = 1;
        double[] d = new double[n];
        double[] Hd = new double[n];
        double rTr, rnewTrnew, cgtol;


        for (int i = 0; i < n; i++) {
            s[i] = 0;
            r[i] = -g[i];
            d[i] = r[i];
        }
        cgtol = 0.1 * euclideanNorm(g);


        int cg_iter = 0;
        rTr = dot(r, r);


        while (true) {
            if (euclideanNorm(r) <= cgtol) break;
            cg_iter++;
            fun_obj.Hv(d, Hd);


            double alpha = rTr / dot(d, Hd);
            daxpy(alpha, d, s);
            if (euclideanNorm(s) > delta) {
                Linear.info("cg reaches trust region boundary");
                alpha = -alpha;
                daxpy(alpha, d, s);


                double std = dot(s, d);
                double sts = dot(s, s);
                double dtd = dot(d, d);
                double dsq = delta * delta;
                double rad = Math.Sqrt(std * std + dtd * (dsq - sts));
                if (std >= 0)
                    alpha = (dsq - sts) / (std + rad);
                else
                    alpha = (rad - std) / dtd;
                daxpy(alpha, d, s);
                alpha = -alpha;
                daxpy(alpha, Hd, r);
                break;
            }
            alpha = -alpha;
            daxpy(alpha, Hd, r);
            rnewTrnew = dot(r, r);
            double beta = rnewTrnew / rTr;
            scale(beta, d);
            daxpy(one, r, d);
            rTr = rnewTrnew;
        }


        return (cg_iter);
    }


    /**
     * constant times a vector plus a vector
     *
     * <pre>
     * vector2 += constant * vector1
     * </pre>
     *
     * @since 1.8
     */
    private static void daxpy(double constant, double[] vector1, double[] vector2) {
        if (constant == 0) return;


        Debug.Assert(vector1.Length == vector2.Length);
        for (int i = 0; i < vector1.Length; i++) {
            vector2[i] += constant * vector1[i];
        }
    }


    /**
     * returns the dot product of two vectors
     *
     * @since 1.8
     */
    private static double dot(double[] vector1, double[] vector2) {


        double product = 0;
        Debug.Assert(vector1.Length == vector2.Length);
        for (int i = 0; i < vector1.Length; i++) {
            product += vector1[i] * vector2[i];
        }
        return product;


    }


    /**
     * returns the euclidean norm of a vector
     *
     * @since 1.8
     */
    private static double euclideanNorm(double[] vector) {


        int n = vector.Length;


        if (n < 1) {
            return 0;
        }


        if (n == 1) {
            return Math.Abs(vector[0]);
        }


        // this algorithm is (often) more accurate than just summing up the squares and taking the square-root afterwards


        double scale = 0; // scaling factor that is factored out
        double sum = 1; // basic sum of squares from which scale has been factored out
        for (int i = 0; i < n; i++) {
            if (vector[i] != 0) {
                double abs = Math.Abs(vector[i]);
                // try to get the best scaling factor
                if (scale < abs) {
                    double t = scale / abs;
                    sum = 1 + sum * (t * t);
                    scale = abs;
                } else {
                    double t = abs / scale;
                    sum += t * t;
                }
            }
        }


        return scale * Math.Sqrt(sum);
    }


    /**
     * scales a vector by a constant
     *
     * @since 1.8
     */
    private static void scale(double constant, double[] vector) {
        if (constant == 1.0) return;
        for (int i = 0; i < vector.Length; i++) {
            vector[i] *= constant;
        }
    }
}

}
