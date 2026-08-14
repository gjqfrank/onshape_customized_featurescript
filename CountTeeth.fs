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

        // ---------- 5. 角度直方图自相关(主方法) -----------------------------
        // 边采样只覆盖半圈(齿顶平面边多, 渐开线齿侧边少), 剖面法失效.
        // 改用所有外缘采样点(顶点+边采样)构建角度直方图(每角度桶的样本数),
        // 然后做圆周自相关: 对候选齿数N, 把所有点角度平移360/N的倍数后,
        // 计算与原始直方图的重合度. 真齿数N会使重合度最大(周期性最强).
        // 这种方法不依赖完整剖面, 只要每齿都有采样点就能工作.
        var tipR = globalMaxR;

        // 过滤内孔/轮毂: 只留 radius > 0.5*tipR 的样本
        var outerSamples = [];
        for (var sd in allSampleData)
        {
            if (sd.radius > tipR * 0.5)
                outerSamples = append(outerSamples, sd);
        }

        // 也加入顶点(齿尖顶点角度精确)
        var allVertices = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.VERTEX));
        var vertexMaxR = 0 * meter;
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var r = radialDistance(pt, axisOrigin, axisDir);
                var d = pt - axisOrigin;
                var proj = d - dot(d, axisDir) * axisDir;
                var angle = atan2(dot(proj, refV), dot(proj, refU)) / degree;
                if (angle < 0)
                    angle += 360;
                if (r > tipR * 0.5)
                    outerSamples = append(outerSamples, { "angle" : angle * degree, "radius" : r });
                if (r > vertexMaxR) vertexMaxR = r;
            }
        }

        if (size(outerSamples) < 4)
            throw "No outer samples. tipR: " ~ toString(tipR);

        // 构建角度直方图(1°一桶, 360桶)
        var NB = 360;
        var angHisto = [];
        for (var i = 0; i < NB; i += 1)
            angHisto = append(angHisto, 0);
        for (var sd in outerSamples)
        {
            var angleDeg = sd.angle / degree;
            if (angleDeg < 0)
                angleDeg += 360;
            var bin = floor(angleDeg / 360 * NB);
            if (bin >= NB) bin = NB - 1;
            if (bin < 0) bin = 0;
            angHisto[bin] += 1;
        }

        var coveredBins = 0;
        for (var i = 0; i < NB; i += 1)
            if (angHisto[i] > 0) coveredBins += 1;

        // 自相关: 对候选齿数 N=4..60, 计算周期 360/N 的自相关分数
        // 分数 = sum(histo[i] * histo[(i + shift) mod NB]) / (sum(histo[i]^2))
        // shift = NB / N (一个齿距对应的桶数)
        // 真齿数时, shift 把每齿的点对齐到下一齿, 重合度高 → 分数高
        var bestTeeth = 1;
        var bestScore = 0.0;
        var scores = []; // 诊断: [N]=score
        for (var N = 4; N <= 60; N += 1)
        {
            var shift = floor(NB / N + 0.5);
            if (shift < 1) shift = 1;
            var numer = 0.0;
            var denom = 0.0;
            for (var i = 0; i < NB; i += 1)
            {
                var j = (i + shift) % NB;
                numer += angHisto[i] * angHisto[j];
                denom += angHisto[i] * angHisto[i];
            }
            var score = 0.0;
            if (denom > 0)
                score = numer / denom;
            scores = append(scores, score);
            if (score > bestScore)
            {
                bestScore = score;
                bestTeeth = N;
            }
        }

        var teeth = bestTeeth;
        var rawHighRegions = 0;
        var bigGaps = 0;
        var highBinCount = 0;
        var minGap = 0;
        var maxGap = 0;
        var dedendumR = 0 * meter;
        var thresh = 0 * meter;
        var amplitude = 0 * meter;
        var rootR = 0 * meter;
        // ---------- 5b. 红点标出齿尖顶点(仅可视化, 不参与计数) -------------
        var vertexPoints = [];
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var r = radialDistance(pt, axisOrigin, axisDir);
                vertexPoints = append(vertexPoints, { "point" : pt, "radius" : r });
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
        // 显示最佳分数附近几个候选齿数的分数, 便于判断
        var scoreStr = "";
        for (var i = 0; i < size(scores); i += 1)
        {
            var N = i + 4;
            if (N >= bestTeeth - 2 && N <= bestTeeth + 2)
                scoreStr = scoreStr ~ toString(N) ~ ":" ~ toString(round(scores[i] * 1000) / 1000) ~ " ";
        }
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | BestScore: " ~ toString(round(bestScore * 1000) / 1000)
            ~ " | Scores: " ~ scoreStr
            ~ " | CoveredBins: " ~ toString(coveredBins)
            ~ " | Outer samples: " ~ toString(size(outerSamples))
            ~ " | Tip vertices: " ~ toString(tipVertexCount)
            ~ " | Tip R: " ~ toString(tipR)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
