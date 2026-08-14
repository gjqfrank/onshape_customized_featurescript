FeatureScript 3044;

import(path : "onshape/std/geometry.fs", version : "3044.0");

// ===========================================================================
// Count Teeth —— 识别带轮(pulley) / 链轮(sprocket) / 齿轮(gear) 的齿数
//
// 新方案(只需选一个 part):
//   1. 自动找回转轴: 找所有圆柱面, 聚类同心轴, 选包含最多圆柱面的轴
//      (回转体上有多个同心圆柱面: 内孔、轮毂、齿根圆等)
//   2. 几何中心: bbox 中心投影到轴上
//   3. 标识轴(黄线)和中心(黄点)
//   4. 全局径向采样: 遍历所有边, 按角度分360桶, 每桶取最大半径
//      → "角度-最大半径"曲线的周期性峰值 = 齿数
//      (不依赖边环, 不受减重孔/倒圆干扰, 因为齿顶半径永远最大)
// ===========================================================================

// 点到轴线的径向距离
function radialDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(d - dot(d, axisDir) * axisDir);
}

// 点到轴线的垂直距离(用于判断两轴是否同心)
function pointToAxisDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(cross(d, axisDir));
}

// (齿数由径向剖面的滞回阈值上升沿计数决定; 顶点仅用于红点可视化)

annotation { "Feature Type Name" : "Count Teeth", "Feature Type Description" : "Count teeth of a pulley or sprocket" }
export const countTeeth = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Pulley or sprocket part", "Filter" : EntityType.BODY && BodyType.SOLID, "MaxNumberOfPicks" : 1 }
        definition.part is Query;

        annotation { "Name" : "Rename part using tooth count", "Default" : false }
        definition.rename is boolean;

        annotation { "Name" : "Name prefix", "Default" : "Pulley" }
        definition.namePrefix is string;
    }
    {
        // ---------- 1. 自动找回转轴 -----------------------------------------
        // 找所有面, 尝试 evAxis 找圆柱面, 收集时去重(方向平行+原点共线视为同一个)
        var allFaces = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.FACE));

        var axes = []; // 去重后的轴列表
        var counts = []; // 每根轴出现的次数
        var tol = 0.01 * meter; // 1cm 同心容差

        for (var face in allFaces)
        {
            try silent
            {
                var ax = evAxis(context, { "axis" : face });
                // 检查是否已有近似轴
                var found = false;
                for (var k = 0; k < size(axes); k += 1)
                {
                    if (abs(dot(axes[k].direction, ax.direction)) > 0.999 &&
                        pointToAxisDistance(axes[k].origin, ax.origin, ax.direction) < tol)
                    {
                        counts[k] += 1;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    axes = append(axes, ax);
                    counts = append(counts, 1);
                }
            }
        }

        if (size(axes) == 0)
            throw "No cylindrical face found, cannot auto-detect rotation axis.";

        // 选出现次数最多的轴(回转轴上有最多同心圆柱面: 内孔、轮毂、齿根圆等)
        var bestCount = counts[0];
        var bestAxis = axes[0];
        for (var k = 1; k < size(axes); k += 1)
        {
            if (counts[k] > bestCount)
            {
                bestCount = counts[k];
                bestAxis = axes[k];
            }
        }

        var axisOrigin is Vector = bestAxis.origin;
        var axisDir is Vector = bestAxis.direction;

        // ---------- 2. 几何中心 ---------------------------------------------
        var bbox = evBox3d(context, { "topology" : definition.part });
        var bboxCenter = (bbox.minCorner + bbox.maxCorner) / 2;
        // 投影到轴上
        var centerOnAxis = axisOrigin + dot(bboxCenter - axisOrigin, axisDir) * axisDir;

        // ---------- 3. 标识轴和中心 -----------------------------------------
        // 黄色画轴(用线)和中心点
        try silent { debug(context, line(centerOnAxis, axisDir), DebugColor.YELLOW); }
        debug(context, centerOnAxis, DebugColor.YELLOW);

        // ---------- 4. 全局径向采样 -----------------------------------------
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        if (size(allEdges) == 0)
            throw "No edges found, cannot sample.";

        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        // 每边采样数: 总采样约2880点, 每边最少16点(提高角度分辨率, 减少空桶)
        var samplesPerEdge = floor(2880 / size(allEdges));
        if (samplesPerEdge < 16)
            samplesPerEdge = 16;

        // 第一遍: 收集所有采样点, 找全局最大半径
        var allSampleData = [];
        var globalMaxR = 0 * meter;
        for (var edge in allEdges)
        {
            for (var i = 0; i < samplesPerEdge; i += 1)
            {
                var t = (i + 0.5) / samplesPerEdge;
                try silent
                {
                    var pt = evEdgeTangentLine(context, { "edge" : edge, "parameter" : t }).origin;
                    var r = radialDistance(pt, axisOrigin, axisDir);
                    var d = pt - axisOrigin;
                    var proj = d - dot(d, axisDir) * axisDir;
                    var angle = atan2(dot(proj, refV), dot(proj, refU));
                    if (angle < 0)
                        angle += 2 * PI;
                    allSampleData = append(allSampleData, { "angle" : angle, "radius" : r });
                    if (r > globalMaxR) globalMaxR = r;
                }
            }
        }

        if (size(allSampleData) < 4)
            throw "Sampling failed. Samples: " ~ toString(size(allSampleData)) ~ " / Edges: " ~ toString(size(allEdges));

        // ---------- 5. 自适应阈值 + 连续高区计数(主方法) -------------------
        // 问题: max-per-bin 剖面的最小值是内孔(boer)半径, 不是齿根圆(dedendum),
        //   导致阈值算错. 解决:
        //   a. tipR = 全局最大半径(齿顶圆)
        //   b. 过滤掉内孔/轮毂样本: 只保留 radius > 0.5*tipR 的样本(外缘齿廓区)
        //   c. dedendumR = 过滤后样本的最小半径(真正的齿根圆)
        //   d. thresh = (tipR + dedendumR) / 2 (齿顶与齿根的中线)
        //   e. 每角度桶取max, 标记 bin > thresh 为"高"
        //   f. 数连续高区个数, 跨过窄低缝(<3°)合并, 忽略窄高区(<3°)噪声
        //   对平顶齿(整段高=1区)和尖顶齿(单峰=1区)都鲁棒
        var tipR = globalMaxR;

        // 过滤内孔/轮毂: 只留 radius > 0.5*tipR 的样本
        var outerSamples = [];
        for (var sd in allSampleData)
        {
            if (sd.radius > tipR * 0.5)
                outerSamples = append(outerSamples, sd);
        }
        if (size(outerSamples) < 4)
            throw "No outer-profile samples after bore filter. tipR: " ~ toString(tipR);

        // dedendumR = 外缘样本的最小半径(真齿根圆)
        var dedendumR = outerSamples[0].radius;
        for (var sd in outerSamples)
        {
            if (sd.radius < dedendumR) dedendumR = sd.radius;
        }

        var amplitude = tipR - dedendumR;
        var thresh = dedendumR + amplitude * 0.5;

        // 每桶取最大半径(只用外缘样本)
        var NBUCKET = 360;
        var profile = [];
        var hasSample = [];
        for (var i = 0; i < NBUCKET; i += 1)
        {
            profile = append(profile, dedendumR);
            hasSample = append(hasSample, false);
        }
        for (var sd in outerSamples)
        {
            var angleDeg = sd.angle / degree;
            if (angleDeg < 0)
                angleDeg += 360;
            var bin = floor(angleDeg / 360 * NBUCKET);
            if (bin >= NBUCKET)
                bin = NBUCKET - 1;
            if (bin < 0)
                bin = 0;
            if (sd.radius > profile[bin])
            {
                profile[bin] = sd.radius;
                hasSample[bin] = true;
            }
        }

        // 填补空桶(用 dedendumR, 因为缺采样的角度大概率是齿根区)
        for (var i = 0; i < NBUCKET; i += 1)
        {
            if (!hasSample[i])
            {
                profile[i] = dedendumR;
            }
        }

        // 布尔剖面: bin 高 = profile[bin] > thresh
        var high = [];
        var highBinCount = 0;
        for (var i = 0; i < NBUCKET; i += 1)
        {
            var h = profile[i] > thresh;
            high = append(high, h);
            if (h) highBinCount += 1;
        }

        // 数连续高区(不合并, 看真实剖面), 收集所有gap长度
        // 注意: 起始点在一个gap中, 遍历一圈后回到起点, 最后的wrap gap和第一个gap是同一个
        // 所以不添加wrap gap, allGaps条数 = 高区数 = 齿数(完整一圈)
        var teeth = 0;
        var allGaps = [];
        {
            // 找第一个低桶作为起点
            var startBin = 0;
            var foundStart = false;
            for (var i = 0; i < NBUCKET; i += 1)
            {
                if (!high[i]) { startBin = i; foundStart = true; break; }
            }
            if (!foundStart)
            {
                teeth = 1;
            }
            else
            {
                var inHigh = false;
                var gapLen = 0;
                for (var k = 0; k < NBUCKET; k += 1)
                {
                    var i = (startBin + k) % NBUCKET;
                    if (high[i])
                    {
                        if (!inHigh)
                        {
                            teeth += 1;
                            if (gapLen > 0)
                                allGaps = append(allGaps, gapLen);
                        }
                        inHigh = true;
                        gapLen = 0;
                    }
                    else
                    {
                        if (inHigh)
                            inHigh = false;
                        gapLen += 1;
                    }
                }
                // 不添加wrap gap: 它和第一个gap是同一个, 已计入allGaps[0]
            }
        }
        if (teeth < 1)
            teeth = 1;

        var rawHighRegions = teeth;
        // gap统计: 区分大gap(真齿间)和小gap(噪声), 用于诊断
        var bigGaps = 0;
        var smallGaps = 0;
        var minGap = 999;
        var maxGap = 0;
        for (var g in allGaps)
        {
            if (g > 5) bigGaps += 1;
            else smallGaps += 1;
            if (g < minGap) minGap = g;
            if (g > maxGap) maxGap = g;
        }
        // 如果有小gap(噪声), 用大gap数作为齿数
        if (smallGaps > 0 && bigGaps > 0)
            teeth = bigGaps;

        var rootR = dedendumR;
        // ---------- 5b. 红点标出齿尖顶点(仅可视化, 不参与计数) -------------
        var allVertices = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.VERTEX));
        var vertexMaxR = 0 * meter;
        var vertexPoints = [];
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var r = radialDistance(pt, axisOrigin, axisDir);
                vertexPoints = append(vertexPoints, { "point" : pt, "radius" : r });
                if (r > vertexMaxR) vertexMaxR = r;
            }
        }
        var tipVertexCount = 0;
        for (var vp in vertexPoints)
        {
            if (vp.radius > vertexMaxR * 0.98)
            {
                debug(context, vp.point, DebugColor.RED);
                tipVertexCount += 1;
            }
        }

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | RawRegions: " ~ toString(rawHighRegions)
            ~ " | BigGaps: " ~ toString(bigGaps)
            ~ " | SmallGaps: " ~ toString(smallGaps)
            ~ " | HighBins: " ~ toString(highBinCount)
            ~ " | MinGap: " ~ toString(minGap)
            ~ " | MaxGap: " ~ toString(maxGap)
            ~ " | Tip R: " ~ toString(tipR)
            ~ " | Dedendum R: " ~ toString(dedendumR)
            ~ " | Thresh: " ~ toString(thresh)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
