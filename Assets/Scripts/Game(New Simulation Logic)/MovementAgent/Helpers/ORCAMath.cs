using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// ORCA-Lite: Optimal Reciprocal Collision Avoidance math functions.
/// Dựa trên Van den Berg et al. (2011), "Reciprocal n-Body Collision Avoidance" — UNC Chapel Hill.
/// 
/// Pipeline: Với mỗi cặp agent, tạo half-plane constraint trong velocity space.
/// Giải Linear Programming 2D để tìm velocity gần preferredVelocity nhất mà thỏa mọi constraint.
/// Kết quả: velocity collision-free (đảm bảo toán học).
/// </summary>
public static class ORCAMath
{
    /// <summary>
    /// Đường ràng buộc ORCA (half-plane) trong velocity space 2D.
    /// Vùng hợp lệ nằm bên TRÁI của direction (nhìn theo direction).
    /// </summary>
    public struct Line
    {
        public float2 point;     // Một điểm trên đường ràng buộc
        public float2 direction; // Hướng đường (unit vector)
    }

    /// <summary>
    /// 2D cross product (determinant): a×b = a.x⋅b.y − a.y⋅b.x
    /// Dương khi b nằm bên trái a, âm khi bên phải.
    /// </summary>
    public static float Det(float2 a, float2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    // ==========================================
    // ORCA LINE CREATION
    // ==========================================

    /// <summary>
    /// Tạo ORCA half-plane constraint cho một cặp agent.
    /// 
    /// Thuật toán:
    ///   1. Tính Velocity Obstacle (VO) — tập hợp relative velocities gây va chạm trong τ giây
    ///   2. Tìm vector u ngắn nhất để thoát khỏi VO
    ///   3. Tạo half-plane vuông góc với u, đi qua agentVel + reciprocalFactor×u
    /// 
    /// reciprocalFactor: 0.5 = cả 2 chia đôi trách nhiệm, 1.0 = chỉ mình tránh (neighbor tĩnh)
    /// </summary>
    public static Line CreateAgentLine(
        float2 relPos,          // neighbor.pos - agent.pos (XZ plane)
        float2 agentVel,        // Agent velocity hiện tại (XZ)
        float2 neighborVel,     // Neighbor velocity hiện tại (XZ)
        float combinedRadius,   // agent.radius + neighbor.radius
        float timeHorizon,      // Nhìn trước bao lâu (giây), ví dụ 1.5
        float invDt,            // 1.0 / deltaTime (dùng cho trường hợp overlap)
        float reciprocalFactor) // 0.5 cho moving neighbor, 1.0 cho static
    {
        float2 relVel = agentVel - neighborVel;
        float distSq = math.lengthsq(relPos);
        float combinedRadSq = combinedRadius * combinedRadius;

        Line line;

        if (distSq > combinedRadSq)
        {
            // ══════════════════════════════════════════════
            // CHƯA CHỒNG LẤN — Tính Velocity Obstacle cone
            // ══════════════════════════════════════════════
            float invT = 1.0f / timeHorizon;
            // w = relative velocity trừ đi "scaled position" (tâm cut-off circle)
            float2 w = relVel - invT * relPos;
            float wLenSq = math.lengthsq(w);
            float dot1 = math.dot(w, relPos);

            if (dot1 < 0.0f && dot1 * dot1 > combinedRadSq * wLenSq)
            {
                // ── Project lên CUT-OFF CIRCLE (đáy của VO cone) ──
                float wLen = math.sqrt(wLenSq);
                float2 unitW = w / math.max(wLen, 1e-5f);
                line.direction = new float2(unitW.y, -unitW.x);
                float2 u = (combinedRadius * invT - wLen) * unitW;
                line.point = agentVel + reciprocalFactor * u;
            }
            else
            {
                // ── Project lên CẠNH (leg) của VO cone ──
                float leg = math.sqrt(math.max(distSq - combinedRadSq, 0.0f));

                if (Det(relPos, w) > 0.0f)
                {
                    // Left leg
                    line.direction = new float2(
                        relPos.x * leg - relPos.y * combinedRadius,
                        relPos.x * combinedRadius + relPos.y * leg
                    ) / distSq;
                }
                else
                {
                    // Right leg
                    line.direction = -new float2(
                        relPos.x * leg + relPos.y * combinedRadius,
                        -relPos.x * combinedRadius + relPos.y * leg
                    ) / distSq;
                }

                float dot2 = math.dot(relVel, line.direction);
                float2 u = dot2 * line.direction - relVel;
                line.point = agentVel + reciprocalFactor * u;
            }
        }
        else
        {
            // ══════════════════════════════════════════════
            // ĐÃ CHỒNG LẤN — Thoát khẩn cấp
            // Dùng invDt thay vì invTimeHorizon → constraint cực mạnh
            // ══════════════════════════════════════════════
            float2 w = relVel - invDt * relPos;
            float wLen = math.length(w);

            if (wLen > 1e-4f)
            {
                float2 unitW = w / wLen;
                line.direction = new float2(unitW.y, -unitW.x);
                float2 u = (combinedRadius * invDt - wLen) * unitW;
                line.point = agentVel + reciprocalFactor * u;
            }
            else
            {
                // Hoàn toàn chồng lấn (cùng vị trí) — chọn hướng vuông góc
                float dist = math.sqrt(math.max(distSq, 1e-6f));
                float2 relDir = dist > 1e-4f ? relPos / dist : new float2(1, 0);
                float2 perpDir = new float2(-relDir.y, relDir.x);
                line.direction = perpDir;
                line.point = agentVel + reciprocalFactor * combinedRadius * invDt * (-relDir);
            }
        }

        return line;
    }

    // ==========================================
    // 2D LINEAR PROGRAMMING (Incremental)
    // ==========================================

    /// <summary>
    /// LP1: Tìm điểm gần optVelocity nhất NẰM TRÊN đường lines[lineNo],
    /// thỏa maxSpeed circle VÀ tất cả constraints [0..lineNo-1].
    /// 
    /// Trả về false nếu infeasible (đường nằm ngoài maxSpeed circle hoặc
    /// khoảng hợp lệ [tLeft, tRight] bị thu hẹp thành rỗng).
    /// </summary>
    public static bool LinearProgram1(
        NativeArray<Line> lines, int lineNo,
        float maxSpeed, float2 optVelocity, bool directionOpt,
        ref float2 result)
    {
        Line currentLine = lines[lineNo];

        // Tìm giao điểm của đường lines[lineNo] với maxSpeed circle
        float dotProd = math.dot(currentLine.point, currentLine.direction);
        float disc = dotProd * dotProd + maxSpeed * maxSpeed
                     - math.lengthsq(currentLine.point);

        if (disc < 0.0f)
            return false; // Đường nằm hoàn toàn ngoài maxSpeed circle

        float sqrtDisc = math.sqrt(disc);
        float tLeft = -dotProd - sqrtDisc;
        float tRight = -dotProd + sqrtDisc;

        // Thu hẹp [tLeft, tRight] theo các constraints trước đó
        for (int i = 0; i < lineNo; i++)
        {
            Line prevLine = lines[i];
            float denom = Det(currentLine.direction, prevLine.direction);
            float numer = Det(prevLine.direction, currentLine.point - prevLine.point);

            if (math.abs(denom) <= 1e-5f)
            {
                // Hai đường gần song song
                if (numer < 0.0f) return false;
                continue;
            }

            float t = numer / denom;
            if (denom >= 0.0f)
                tRight = math.min(tRight, t);
            else
                tLeft = math.max(tLeft, t);

            if (tLeft > tRight) return false;
        }

        if (directionOpt)
        {
            // Optimize theo direction (dùng trong LP3)
            if (math.dot(optVelocity, currentLine.direction) > 0.0f)
                result = currentLine.point + tRight * currentLine.direction;
            else
                result = currentLine.point + tLeft * currentLine.direction;
        }
        else
        {
            // Tìm điểm gần optVelocity nhất trên đoạn [tLeft, tRight]
            float t = math.dot(currentLine.direction, optVelocity - currentLine.point);
            result = currentLine.point + math.clamp(t, tLeft, tRight) * currentLine.direction;
        }

        return true;
    }

    /// <summary>
    /// LP2: Thêm constraints lần lượt (incremental).
    /// Bắt đầu với preferredVelocity (clamp trong maxSpeed circle).
    /// Mỗi constraint mới: nếu vi phạm → chiếu lên constraint đó (gọi LP1).
    /// 
    /// Trả về: numLines nếu thành công, hoặc index constraint đầu tiên gây infeasible.
    /// </summary>
    public static int LinearProgram2(
        NativeArray<Line> lines, int numLines,
        float maxSpeed, float2 optVelocity,
        ref float2 result)
    {
        // Khởi tạo: clamp preferredVelocity vào maxSpeed circle
        if (math.lengthsq(optVelocity) > maxSpeed * maxSpeed)
            result = math.normalize(optVelocity) * maxSpeed;
        else
            result = optVelocity;

        for (int i = 0; i < numLines; i++)
        {
            // Kiểm tra constraint i: Det > 0 nghĩa là result nằm bên PHẢI (vi phạm)
            if (Det(lines[i].direction, lines[i].point - result) > 0.0f)
            {
                float2 tempResult = result;
                if (!LinearProgram1(lines, i, maxSpeed, optVelocity, false, ref result))
                {
                    result = tempResult;
                    return i; // Infeasible tại constraint i
                }
            }
        }

        return numLines; // Tất cả constraints thỏa mãn
    }

    /// <summary>
    /// LP3: Fallback khi LP2 thất bại (over-constrained, ví dụ bị bao vây).
    /// Lặp qua constraints và chiếu result lên boundary của constraint bị vi phạm nặng nhất.
    /// Không tối ưu hoàn hảo nhưng đảm bảo velocity khả dụng trong thực tế.
    /// </summary>
    public static void LinearProgram3(
        NativeArray<Line> lines, int numLines, int beginLine,
        float maxSpeed, ref float2 result)
    {
        for (int i = beginLine; i < numLines; i++)
        {
            float pen = Det(lines[i].direction, lines[i].point - result);
            if (pen > 0.0f)
            {
                // Chiếu result lên boundary: outward normal = (d.y, -d.x)
                // result -= outward * pen → đẩy velocity vào vùng hợp lệ (tránh xa neighbor)
                float2 lineNormal = new float2(lines[i].direction.y, -lines[i].direction.x);
                result -= lineNormal * pen;

                // Clamp lại vào maxSpeed circle
                if (math.lengthsq(result) > maxSpeed * maxSpeed)
                    result = math.normalize(result) * maxSpeed;
            }
        }
    }
}
