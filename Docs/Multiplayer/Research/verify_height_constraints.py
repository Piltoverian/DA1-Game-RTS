"""Numerical design experiment only; does not test Unity movement or complete maps."""
import json
import math
from pathlib import Path


def fade(t):
    t = min(1.0, max(0.0, t))
    return t * t * t * (t * (t * 6 - 15) + 10)


def gradient(points, values):
    (x0, y0), (x1, y1), (x2, y2) = points
    h0, h1, h2 = values
    dx1, dy1, dx2, dy2 = x1-x0, y1-y0, x2-x0, y2-y0
    det = dx1*dy2-dx2*dy1
    return ((h1-h0)*dy2-(h2-h0)*dy1)/det, (dx1*(h2-h0)-dx2*(h1-h0))/det


def run(amplitude):
    width, height = 128, 64
    limit = math.tan(math.radians(25))
    heights, residual = {}, {}
    for y in range(height+1):
        for x in range(width+1):
            # Vertex rotation is (W-x,H-y), different from cell rotation.
            edge_distance = min(x, width-x)
            h0 = 4*(1-fade((edge_distance-16)/20))
            mask = fade((edge_distance-36)/8)
            # Analytic low-frequency field for the experiment, NOT production Perlin.
            def sample(px, py):
                return (math.sin(px/6)*math.cos(py/8) + 0.5*math.sin(px/3+py/5))/1.5
            n = (sample(x,y)+sample(width-x,height-y))/2
            heights[x, y] = h0
            residual[x, y] = amplitude*mask*n

    gradients = []
    alpha = 1.0
    for y in range(height):
        for x in range(width):
            # Same diagonal maps to itself under 180-degree rotation.
            for vertices in (((x,y),(x+1,y),(x+1,y+1)), ((x,y),(x+1,y+1),(x,y+1))):
                g0 = gradient(vertices, [heights[p] for p in vertices])
                gn = gradient(vertices, [residual[p] for p in vertices])
                a = sum(v*v for v in gn)
                b = 2*sum(g0[i]*gn[i] for i in range(2))
                c = sum(v*v for v in g0)-limit*limit
                assert c <= 1e-12, "Base ramp itself violates the slope budget"
                if a > 1e-20:
                    upper = (-b+math.sqrt(max(0, b*b-4*a*c)))/(2*a)
                    alpha = min(alpha, upper)
                gradients.append((g0, gn))

    raw_max = max(math.hypot(g0[0]+gn[0],g0[1]+gn[1]) for g0,gn in gradients)
    out = {p: round((heights[p]+alpha*residual[p])*256)/256 for p in heights}
    max_final = 0.0
    for y in range(height):
        for x in range(width):
            for vertices in (((x,y),(x+1,y),(x+1,y+1)), ((x,y),(x+1,y+1),(x,y+1))):
                max_final = max(max_final, math.hypot(*gradient(vertices,[out[p] for p in vertices])))
    symmetry_error = max(abs(out[x,y]-out[width-x,height-y]) for x,y in out)
    return {
        "raw_amplitude_cell_units": amplitude,
        "alpha": alpha,
        "raw_max_slope_degrees": math.degrees(math.atan(raw_max)),
        "quantized_max_slope_degrees": math.degrees(math.atan(max_final)),
        "rotation_height_error": symmetry_error,
        "quantized_slope_passes": max_final <= limit+1e-12,
    }


if __name__ == '__main__':
    results = [run(a) for a in (0.8, 4.0)]
    # Quantization can invalidate a boundary solution; reduce alpha with a margin
    # in production or revalidate and deterministically shrink/retry.
    report = {
        "scope": "Synthetic symmetric ramp/height field; no cliff, resource or Unity runtime test",
        "ramp_delta_cells": 4,
        "ramp_length_cells": 20,
        "analytic_ramp_max_slope_degrees": math.degrees(math.atan(1.875*4/20)),
        "slope_limit_degrees": 25,
        "results": results,
    }
    Path(__file__).with_name('height_constraint_results.json').write_text(json.dumps(report, indent=2),encoding='utf-8')
    print(json.dumps(report, indent=2))
